using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.Capture;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.Overlay;
using ZoomIN.Rendering.Snapshots;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// All GPU and window resources for zooming one monitor. Created and used only on the render
/// thread. <see cref="RenderFrame"/> is the hot path and must not allocate (AGENTS.md 2.3).
/// </summary>
internal sealed class RenderSession : IDisposable
{
    private static readonly Color4 s_transparent = new(0, 0, 0, 0);

    // Owned resources in creation order; disposed in reverse.
    private readonly List<IDisposable> _owned = [];
    private readonly PointerServices _pointer;
    private readonly MonitorOutput _monitor;
    private readonly GpuDevice _gpu;
    private readonly DesktopDuplicator _duplicator;
    private readonly OverlayWindow _overlay;
    private readonly CompositionSwapchain _swapchain;
    private readonly CursorLayer _cursorLayer;
    private readonly HighlightLayer _highlights;
    private readonly DesktopLayer _desktop;
    private readonly SnapshotTaker _snapshots;
    private readonly AnchorController _anchors = new();
    private readonly FrameChangeTracker _changes = new();
    private readonly int _displayGeneration;
    private PhysicalPoint _lastCursor;
    private bool _cursorInside;
    private bool _shown;
    private bool _holdsFrameSlot;

    public RenderSession(PointerServices pointer, string deviceName, SnapshotTaker snapshots, SmartInvertHealth health, ILogger log)
    {
        _pointer = pointer;
        _snapshots = snapshots;

        // Taken before enumerating outputs so a change during construction is not missed.
        _displayGeneration = OverlayWindow.DisplayChangeCount;
        try
        {
            // Phases as child operations of zoomin.render.pipeline_start (startup measurement, M14).
            IDXGIFactory2 factory;
            using (Telemetry.Source.StartActivity("zoomin.render.pipeline_start.device"))
            {
                factory = Own(DXGI.CreateDXGIFactory2<IDXGIFactory2>(false));
                _monitor = Own(MonitorOutput.Find(factory, deviceName));
                _lastCursor = new PhysicalPoint(_monitor.Bounds.X + (_monitor.Width / 2), _monitor.Bounds.Y + (_monitor.Height / 2));
                _gpu = Own(GpuDevice.CreateForAdapter(_monitor.Adapter));
            }

            using (Telemetry.Source.StartActivity("zoomin.render.pipeline_start.capture"))
            {
                _duplicator = Own(new DesktopDuplicator(_gpu, _monitor));
            }

            using (Telemetry.Source.StartActivity("zoomin.render.pipeline_start.overlay"))
            {
                _overlay = Own(new OverlayWindow(_monitor.Bounds));
                _swapchain = Own(new CompositionSwapchain(_gpu, factory, _overlay.Handle, _monitor.Width, _monitor.Height));
            }

            using (Telemetry.Source.StartActivity("zoomin.render.pipeline_start.shaders"))
            {
                var pass = Own(new MagnifyPass(_gpu.Device));
                var cursorPass = Own(new CursorPass(_gpu.Device));
                _cursorLayer = new CursorLayer(cursorPass, pointer.Shapes, pointer.SystemCursor);
                _highlights = new HighlightLayer(cursorPass);
                var smart = Own(SmartInvertLayer.Create(_gpu.Device, _monitor.Width, _monitor.Height, deviceName, health, log));
                _desktop = new DesktopLayer(pass, smart, Own(new GpuPassTimer(_gpu.Device, deviceName, "magnify")));
            }
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

    /// <summary>Some window on this monitor looks different from the rest; per frame, no allocation.</summary>
    public bool FiltersWindows(WindowRegions windows, DisplayEffects effects) =>
        windows.ChangesMonitor(_monitor.Bounds, effects);

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

    /// <summary>
    /// Renders one frame, paced by the compositor; with nothing changed it draws nothing and waits
    /// for the next vertical blank instead (M14 step 3).
    /// </summary>
    /// <exception cref="DisplayChangedException">The pipeline no longer matches the monitors.</exception>
    public FrameOutcome RenderFrame(in FrameRequest request)
    {
        if (OverlayWindow.DisplayChangeCount != _displayGeneration)
        {
            throw new DisplayChangedException();
        }

        // The latency waitable works like a semaphore: one wait per Present. An idle frame keeps the
        // slot it waited for, so the next drawn frame does not block for up to 100 ms.
        if (!_holdsFrameSlot)
        {
            if (!_swapchain.WaitForNextFrame(100))
            {
                return FrameOutcome.Missed;
            }

            _holdsFrameSlot = true;
        }

        // Without zoom or filter the desktop is not copied at all: Windows shows it, ZoomIN only
        // draws on a transparent overlay (pointer page: pointer at 1×, crosshair, locator).
        var newImage = false;
        if (!request.OverlayOnly)
        {
            newImage = _duplicator.Update();

            // Nothing captured yet: draw nothing and keep the overlay hidden instead of showing black.
            if (!_duplicator.HasImage)
            {
                _gpu.Context.ClearRenderTargetView(_swapchain.RenderTarget, s_transparent);
                Present();
                return FrameOutcome.Presented;
            }
        }

        // Read the cursor as late as possible: it decides what is shown under the pointer.
        // A monitor without the mouse keeps its last view (M3 decision).
        var hasCursor = _pointer.Position.TryGetPosition(out var cursor);
        _cursorInside = hasCursor && _monitor.Bounds.Contains(cursor);
        if (_cursorInside)
        {
            _lastCursor = cursor;
        }

        // The anchor is the mouse, or the focus/caret while the user works with the keyboard (M6).
        var anchor = _anchors.Next(new AnchorInput(
            Stopwatch.GetTimestamp(), hasCursor ? cursor : _lastCursor, _cursorInside, _monitor.Bounds,
            request.Mode, request.Zoom, request.Lens, request.Dock, request.Tracking, request.TrackingOptions));

        var frame = ModeGeometry.Compute(request.Mode, _monitor.Bounds, anchor, request.Zoom, request.Lens, request.Dock);
        var shape = _pointer.Shapes.TryGetCurrent(out var handle) ? handle : 0;
        // The desktop layer (and its smoothing) only runs while the desktop is drawn.
        var animating = LocatorActive || (!request.OverlayOnly && _desktop.SmartSettling);
        var changes = _changes.Compare(newImage, request, frame, _cursorInside, _lastCursor, shape,
            animating, _snapshots.IsRequested, _shown);
        if (!changes.NeedsRedraw)
        {
            // All render threads share the one system pointer; another monitor may have switched
            // it meanwhile. Asking again costs nothing when it is already right (lock-free check).
            if (_cursorInside)
            {
                _cursorLayer.KeepSystemCursor();
            }

            _duplicator.WaitForNextRefresh();
            return FrameOutcome.Idle;
        }

        ID3D11ShaderResourceView? weights = null;
        if (request.OverlayOnly)
        {
            _gpu.Context.ClearRenderTargetView(_swapchain.RenderTarget, s_transparent);
        }
        else
        {
            weights = _desktop.Draw(_gpu.Context, _duplicator.ShaderResource, _swapchain.RenderTarget, _swapchain.Width, _swapchain.Height,
                _monitor.Bounds, frame, request.Effects, request.Windows, request.WindowsFiltered, newImage);
        }

        _highlights.Draw(_gpu.Context, _swapchain.RenderTarget, _monitor.Bounds, frame, request.Tracking, request.TrackingOptions);
        LocatorActive = _cursorInside && _cursorLayer.Draw(
            _gpu.Context, _swapchain.RenderTarget, _monitor.Bounds, frame, request.Mode, request.Zoom, _lastCursor,
            request.Effects, request.LocateRequestedAt, request.PointerHidden);
        _snapshots.TakeIfRequested(_gpu.Device, _gpu.Context, request.OverlayOnly ? null : _duplicator.ShaderResource,
            _swapchain.RenderTarget, weights, _monitor.Bounds, frame.Source, request);
        Present();
        _changes.Presented(request, frame, _cursorInside, _lastCursor, shape);

        // Show the window only after the first frame so it never flashes empty.
        if (!_shown)
        {
            _overlay.Show();
            _shown = true;
        }

        return FrameOutcome.Presented;
    }

    public void Dispose()
    {
        for (var i = _owned.Count - 1; i >= 0; i--)
        {
            _owned[i].Dispose();
        }

        _owned.Clear();
    }

    private void Present()
    {
        _swapchain.Present();
        _holdsFrameSlot = false;
    }

    private T Own<T>(T resource)
        where T : IDisposable
    {
        _owned.Add(resource);
        return resource;
    }
}
