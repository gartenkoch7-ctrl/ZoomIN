using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Vortice.Direct3D11;
using Vortice.Mathematics;
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

    private readonly RenderResources _resources;
    private readonly PointerServices _pointer;
    private readonly MonitorOutput _monitor;
    private readonly GpuDevice _gpu;
    private readonly DesktopDuplicator _duplicator;
    private readonly OverlayWindow _overlay;
    private readonly CompositionSwapchain _swapchain;
    private readonly CursorLayer _cursorLayer;
    private readonly HighlightLayer _highlights;
    private readonly OverviewLayer _overview;
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
        _resources = new RenderResources(pointer, deviceName, health, log);

        // Copied once into fields, so the hot path below reads them as before.
        _monitor = _resources.Monitor;
        _gpu = _resources.Gpu;
        _duplicator = _resources.Duplicator;
        _overlay = _resources.Overlay;
        _swapchain = _resources.Swapchain;
        _cursorLayer = _resources.CursorLayer;
        _highlights = _resources.Highlights;
        _overview = _resources.OverviewLayer;
        _desktop = _resources.Desktop;
        _lastCursor = new PhysicalPoint(_monitor.Bounds.X + (_monitor.Width / 2), _monitor.Bounds.Y + (_monitor.Height / 2));
    }

    /// <summary>The overlay is on screen.</summary>
    public bool IsVisible => _shown;

    /// <summary>The pointer locator was still animating in the last frame.</summary>
    public bool LocatorActive { get; private set; }

    /// <summary>Some window on this monitor looks different from the rest; per frame, no allocation.</summary>
    public bool FiltersWindows(WindowRegions windows, DisplayEffects effects) =>
        windows.ChangesMonitor(_monitor.Bounds, effects);

    /// <summary>Hides the overlay but keeps every resource warm for an instant restart.</summary>
    /// <param name="keepView">A temporary level holds the monitor: the view before it stays valid for an overview.</param>
    public void Hide(bool keepView = false)
    {
        _overlay.Hide();
        _shown = false;
        if (!keepView)
        {
            _overview.Forget();
        }
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
            request.AnchorMode, request.Zoom, request.Lens, request.Dock, request.Tracking, request.TrackingOptions, request.Placement));

        var frame = ModeGeometry.Compute(request.Mode, _monitor.Bounds, anchor, request.Zoom, request.Lens, request.Dock);
        _overview.Update(frame, request.Overview, _monitor.Bounds, hasCursor ? cursor : _lastCursor, request.OverviewLevel, request.Lens, request.Dock,
            request.TrackingOptions.MouseFollow, request.ViewHeld);
        var shape = _pointer.Shapes.TryGetCurrent(out var handle) ? handle : 0;
        // The desktop layer (and its smoothing) only runs while the desktop is drawn.
        // The overview frame also counts: it follows the mouse even while that is on another monitor.
        var animating = LocatorActive || (!request.OverlayOnly && _desktop.SmartSettling) || _overview.NeedsRedraw;
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
        _overview.Draw(_gpu.Context, _swapchain.RenderTarget, _monitor.Bounds, frame);
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

    public void Dispose() => _resources.Dispose();

    private void Present()
    {
        _swapchain.Present();
        _holdsFrameSlot = false;
    }
}
