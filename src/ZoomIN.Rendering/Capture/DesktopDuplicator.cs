using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.Interop;

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
    /// Pulls a new frame if one is available, without blocking. Recreates the duplication after
    /// ACCESS_LOST (mode change, lock screen, UAC prompt).
    /// </summary>
    public void Update()
    {
        if (_duplication is null && !TryRecreate())
        {
            return;
        }

        var duplication = _duplication!.NativePointer;
        var hr = DxgiInterop.AcquireNextFrame(duplication, 0, out var info, out var resource);
        if (hr == DxgiInterop.WaitTimeout)
        {
            return;
        }

        if (hr == DxgiInterop.AccessLost)
        {
            _duplication.Dispose();
            _duplication = null;
            return;
        }

        Marshal.ThrowExceptionForHR(hr);
        try
        {
            // LastPresentTime == 0 means only the pointer moved; the image is unchanged.
            if (info.LastPresentTime != 0
                && Marshal.QueryInterface(resource, in DxgiInterop.D3D11ResourceId, out var texture) >= 0)
            {
                DxgiInterop.CopyResource(_gpu.Context.NativePointer, _copy.NativePointer, texture);
                Marshal.Release(texture);
            }
        }
        finally
        {
            Marshal.Release(resource);
            DxgiInterop.ReleaseFrame(duplication);
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
