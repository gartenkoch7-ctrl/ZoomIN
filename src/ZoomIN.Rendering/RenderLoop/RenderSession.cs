using Vortice.DXGI;
using Vortice.Mathematics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
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
    // Frame around lens and docked view, in physical pixels.
    private const double FrameThickness = 4;
    private static readonly Color4 s_transparent = new(0, 0, 0, 0);

    // Owned resources in creation order; disposed in reverse.
    private readonly List<IDisposable> _owned = [];
    private readonly PointerServices _pointer;
    private readonly MonitorOutput _monitor;
    private readonly GpuDevice _gpu;
    private readonly DesktopDuplicator _duplicator;
    private readonly OverlayWindow _overlay;
    private readonly CompositionSwapchain _swapchain;
    private readonly MagnifyPass _pass;
    private readonly CursorLayer _cursorLayer;
    private readonly int _displayGeneration;
    private PhysicalPoint _lastCursor;
    private bool _cursorInside;
    private bool _shown;

    public RenderSession(PointerServices pointer, string deviceName)
    {
        _pointer = pointer;

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
            _cursorLayer = Own(new CursorLayer(_gpu.Device, pointer.Shapes, pointer.SystemCursor));
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>The overlay is on screen.</summary>
    public bool IsVisible => _shown;

    /// <summary>The pointer locator was still animating in the last frame.</summary>
    public bool LocatorActive { get; private set; }

    /// <summary>Hides the overlay but keeps every resource warm for an instant restart.</summary>
    public void Hide()
    {
        _overlay.Hide();
        _shown = false;
        if (_cursorInside)
        {
            _cursorLayer.ShowSystemCursor();
        }
    }

    /// <summary>Renders one frame, paced by the compositor.</summary>
    /// <returns>False if the compositor did not request a frame in time.</returns>
    /// <exception cref="DisplayChangedException">The pipeline no longer matches the monitors.</exception>
    public bool RenderFrame(ZoomMode mode, double zoom, LensSize lens, DockLayout dock, DisplayEffects effects, long locateRequestedAt)
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
        _cursorInside = _pointer.Position.TryGetPosition(out var cursor) && _monitor.Bounds.Contains(cursor);
        if (_cursorInside)
        {
            _lastCursor = cursor;
        }

        var frame = ModeGeometry.Compute(mode, _monitor.Bounds, _lastCursor, zoom, lens, dock);
        Draw(frame, effects);
        LocatorActive = _cursorInside && _cursorLayer.Draw(
            _gpu.Context, _swapchain.RenderTarget, _monitor.Bounds, frame, mode, zoom, _lastCursor, effects, locateRequestedAt);
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

    private void Draw(in ModeFrame frame, DisplayEffects effects)
    {
        var monitor = _monitor.Bounds;
        var matrix = effects.Matrix;
        if (!frame.HasFrame)
        {
            _pass.Draw(_gpu.Context, _duplicator.ShaderResource, _swapchain.RenderTarget, _swapchain.Width, _swapchain.Height,
                MagnifyConstants.Fullscreen(frame.Source, monitor, matrix, effects.Smoothing));
            return;
        }

        // Lens/docked: everything outside the target stays transparent (and click-through).
        _gpu.Context.ClearRenderTargetView(_swapchain.RenderTarget, s_transparent);
        var viewport = Expand(frame.Target, FrameThickness, monitor);
        var constants = MagnifyConstants.WithFrame(frame.Source, monitor, frame.Target, viewport, FrameColor.Default, matrix, effects.Smoothing);
        _pass.Draw(
            _gpu.Context,
            _duplicator.ShaderResource,
            _swapchain.RenderTarget,
            (float)(viewport.X - monitor.X),
            (float)(viewport.Y - monitor.Y),
            (float)viewport.Width,
            (float)viewport.Height,
            constants);
    }

    private static PhysicalRect Expand(PhysicalRect rect, double by, PhysicalRect within)
    {
        var x = Math.Max(rect.X - by, within.X);
        var y = Math.Max(rect.Y - by, within.Y);
        return new PhysicalRect(x, y, Math.Min(rect.Right + by, within.Right) - x, Math.Min(rect.Bottom + by, within.Bottom) - y);
    }

    private T Own<T>(T resource)
        where T : IDisposable
    {
        _owned.Add(resource);
        return resource;
    }
}
