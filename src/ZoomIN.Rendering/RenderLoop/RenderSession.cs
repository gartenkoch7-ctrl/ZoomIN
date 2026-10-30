using Vortice.DXGI;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Capture;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.Overlay;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// All GPU and window resources for zooming one monitor. Created and used only on the render
/// thread. <see cref="RenderFrame"/> is the hot path and must not allocate (AGENTS.md 2.3).
/// </summary>
internal sealed class RenderSession : IDisposable
{
    // Owned resources in creation order; disposed in reverse.
    private readonly List<IDisposable> _owned = [];
    private readonly ICursorSource _cursor;
    private readonly MonitorOutput _monitor;
    private readonly GpuDevice _gpu;
    private readonly DesktopDuplicator _duplicator;
    private readonly OverlayWindow _overlay;
    private readonly CompositionSwapchain _swapchain;
    private readonly MagnifyPass _pass;
    private readonly int _displayGeneration;
    private PhysicalPoint _lastCursor;
    private bool _shown;

    public RenderSession(ICursorSource cursor, string deviceName)
    {
        _cursor = cursor;

        // Taken before enumerating outputs so a change during construction is not missed.
        _displayGeneration = OverlayWindow.DisplayChangeCount;
        try
        {
            var factory = Own(DXGI.CreateDXGIFactory2<IDXGIFactory2>(false));
            _monitor = Own(MonitorOutput.Find(factory, deviceName));
            _lastCursor = new PhysicalPoint(_monitor.Bounds.X + (_monitor.Width / 2), _monitor.Bounds.Y + (_monitor.Height / 2));
            _gpu = Own(GpuDevice.CreateForAdapter(_monitor.Adapter));
            _duplicator = Own(new DesktopDuplicator(_gpu, _monitor));
            _overlay = Own(new OverlayWindow(_monitor.Bounds));
            _swapchain = Own(new CompositionSwapchain(_gpu, factory, _overlay.Handle, _monitor.Width, _monitor.Height));
            _pass = Own(new MagnifyPass(_gpu.Device));
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>The overlay is on screen (zoom is visible).</summary>
    public bool IsVisible => _shown;

    /// <summary>Hides the overlay but keeps every resource warm for an instant restart.</summary>
    public void Hide()
    {
        _overlay.Hide();
        _shown = false;
    }

    /// <summary>Renders one frame, paced by the compositor.</summary>
    /// <returns>False if the compositor did not request a frame in time.</returns>
    /// <exception cref="DisplayChangedException">The pipeline no longer matches the monitors.</exception>
    public bool RenderFrame(double zoom)
    {
        if (OverlayWindow.DisplayChangeCount != _displayGeneration)
        {
            throw new DisplayChangedException();
        }

        if (!_swapchain.WaitForNextFrame(100))
        {
            return false;
        }

        _duplicator.Update();

        // Read the cursor as late as possible: it decides what is shown under the pointer.
        // A monitor without the mouse keeps its last view (M3 decision).
        if (_cursor.TryGetPosition(out var cursor) && _monitor.Bounds.Contains(cursor))
        {
            _lastCursor = cursor;
        }

        var source = ViewportCalculator.SourceRect(_monitor.Bounds, _lastCursor, zoom);
        var constants = MagnifyConstants.FromSourceRect(source, _monitor.Bounds);

        _pass.Draw(_gpu.Context, _duplicator.ShaderResource, _swapchain.RenderTarget, _swapchain.Width, _swapchain.Height, constants);
        _swapchain.Present();

        // Show the window only after the first frame so it never flashes empty.
        if (!_shown)
        {
            _overlay.Show();
            _shown = true;
        }

        return true;
    }

    public void Dispose()
    {
        for (var i = _owned.Count - 1; i >= 0; i--)
        {
            _owned[i].Dispose();
        }

        _owned.Clear();
    }

    private T Own<T>(T resource)
        where T : IDisposable
    {
        _owned.Add(resource);
        return resource;
    }
}
