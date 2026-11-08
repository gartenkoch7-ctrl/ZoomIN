using System.Diagnostics;
using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.Interop;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.Rendering.Capture;

/// <summary>
/// Captures one monitor via DXGI Desktop Duplication into a texture we own, so the duplication
/// frame can be released immediately. Windows with WDA_EXCLUDEFROMCAPTURE (our overlay) are not
/// part of the image, which prevents feedback (docs/kb/capture-ausschluss.md).
/// <see cref="Update"/> runs every frame and does not allocate (raw COM calls, see DxgiInterop).
/// </summary>
internal sealed class DesktopDuplicator : IDisposable
{
    private readonly GpuDevice _gpu;
    private readonly MonitorOutput _monitor;
    private readonly ID3D11Texture2D _copy;
    private IDXGIOutputDuplication? _duplication;
    private int _quickReturns;

    public DesktopDuplicator(GpuDevice gpu, MonitorOutput monitor)
    {
        _gpu = gpu;
        _monitor = monitor;
        _copy = gpu.Device.CreateTexture2D(new Texture2DDescription(
            Format.B8G8R8A8_UNorm,
            (uint)monitor.Width,
            (uint)monitor.Height,
            arraySize: 1,
            mipLevels: 1,
            bindFlags: BindFlags.ShaderResource));
        ShaderResource = gpu.Device.CreateShaderResourceView(_copy);
        _duplication = monitor.Output.DuplicateOutput(gpu.Device);
    }

    /// <summary>Latest desktop image. Keeps the previous image while nothing changes.</summary>
    public ID3D11ShaderResourceView ShaderResource { get; }

    /// <summary>
    /// False until the first desktop image was copied. Until then the copy is empty and would be
    /// shown as a black screen, e.g. right after a start on the secure desktop (ADR-0016).
    /// </summary>
    public bool HasImage { get; private set; }

    /// <summary>
    /// Pulls a new frame if one is available, without blocking. Recreates the duplication after
    /// ACCESS_LOST (mode change, lock screen, UAC prompt).
    /// </summary>
    /// <returns>True if a new desktop image was copied (the idle check of M14 step 3).</returns>
    public bool Update()
    {
        if (_duplication is null && !TryRecreate())
        {
            return false;
        }

        var duplication = _duplication!.NativePointer;
        var hr = DxgiInterop.AcquireNextFrame(duplication, 0, out var info, out var resource);
        if (hr == DxgiInterop.WaitTimeout)
        {
            return false;
        }

        if (hr == DxgiInterop.AccessLost)
        {
            _duplication.Dispose();
            _duplication = null;
            return false;
        }

        Marshal.ThrowExceptionForHR(hr);
        var copied = false;
        try
        {
            // LastPresentTime == 0 means only the pointer moved; the image is unchanged.
            if (info.LastPresentTime != 0
                && Marshal.QueryInterface(resource, in DxgiInterop.D3D11ResourceId, out var texture) >= 0)
            {
                DxgiInterop.CopyResource(_gpu.Context.NativePointer, _copy.NativePointer, texture);
                Marshal.Release(texture);
                HasImage = true;
                copied = true;
            }
        }
        finally
        {
            Marshal.Release(resource);
            DxgiInterop.ReleaseFrame(duplication);
        }

        return copied;
    }

    /// <summary>
    /// Waits for the next vertical blank of this monitor: the clock of the idle loop, which no
    /// longer waits on the swapchain because nothing is presented (M14 step 3). A change is then
    /// drawn at the latest one refresh later.
    /// </summary>
    public void WaitForNextRefresh()
    {
        // WaitForVBlank fails or returns at once when the output cannot wait (monitor off, remote
        // session): then a short sleep keeps the loop from spinning a core. One quick return is
        // normal (the blank was just due), so only a failure or several in a row count.
        var start = Stopwatch.GetTimestamp();
        var failed = DxgiInterop.WaitForVBlank(_monitor.Output.NativePointer) < 0;
        _quickReturns = Stopwatch.GetElapsedTime(start).TotalMilliseconds < 0.5 ? Math.Min(_quickReturns + 1, 3) : 0;
        if (failed || _quickReturns >= 3)
        {
            Thread.Sleep(8);
        }
    }

    public void Dispose()
    {
        _duplication?.Dispose();
        ShaderResource.Dispose();
        _copy.Dispose();
    }

    private bool TryRecreate()
    {
        try
        {
            // ACCESS_LOST is also raised for resolution and orientation changes; our copy texture
            // and the overlay would no longer fit, so the whole pipeline has to be rebuilt.
            var r = _monitor.Output.Description.DesktopCoordinates;
            if (r.Left != _monitor.Bounds.X || r.Top != _monitor.Bounds.Y
                || r.Right - r.Left != _monitor.Width || r.Bottom - r.Top != _monitor.Height)
            {
                throw new DisplayChangedException("Monitor bounds changed.");
            }

            _duplication = _monitor.Output.DuplicateOutput(_gpu.Device);
            return true;
        }
        catch (SharpGen.Runtime.SharpGenException)
        {
            // Not available yet (e.g. secure desktop still active): retry next frame.
            return false;
        }
    }
}
