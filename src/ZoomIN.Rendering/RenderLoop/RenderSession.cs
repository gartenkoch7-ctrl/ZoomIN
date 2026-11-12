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
using ZoomIN.Rendering.Freeze;
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
    private readonly FrozenLayer _frozen;
    private readonly SnapshotTaker _snapshots;
    private readonly AnchorController _anchors = new();
    private readonly FrameChangeTracker _changes = new();
    private readonly int _displayGeneration;
    private PhysicalPoint _lastCursor;
    private PhysicalPoint _lastAnchor;
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
        _frozen = _resources.Frozen;
        _lastCursor = new PhysicalPoint(_monitor.Bounds.X + (_monitor.Width / 2), _monitor.Bounds.Y + (_monitor.Height / 2));
        _lastAnchor = _lastCursor;
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

        // Hidden means not frozen any more (a frozen monitor never sleeps): give a still image back now,
        // not when the monitor happens to render again.
        _frozen.Begin(null);
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
        // A frozen monitor with a still image reads no new capture frame once the image is taken (G2).
        var frozen = request.Frozen;
        _frozen.Begin(frozen);
        var newImage = false;
        if (!request.OverlayOnly && !_frozen.HoldsStill(frozen))
        {
            newImage = _duplicator.Update();

            // Nothing captured yet: draw nothing and keep the overlay hidden instead of showing black. Also
            // while a still image waits for an up-to-date copy (after Win+L): never an old desktop (G2).
            if (!_duplicator.HasImage || (request.FrozenStill && !_duplicator.IsCurrent))
            {
                _gpu.Context.ClearRenderTargetView(_swapchain.RenderTarget, s_transparent);
                Present();
                return FrameOutcome.Presented;
            }

            newImage |= _frozen.TryCapture(frozen, _gpu.Context, _duplicator.Texture, _duplicator.IsCurrent, request.Windows);
        }

        var image = _frozen.Desktop(_duplicator.ShaderResource);

        // Read the cursor as late as possible: it decides what is shown under the pointer.
        // A monitor without the mouse keeps its last view (M3 decision); a frozen one never draws it.
        var hasCursor = _pointer.Position.TryGetPosition(out var cursor);
        var onMonitor = hasCursor && _monitor.Bounds.Contains(cursor);
        var showSystemPointer = _frozen.NeedsSystemPointer(_cursorInside, onMonitor);
        _cursorInside = onMonitor && frozen is null;
        if (_cursorInside)
        {
            _lastCursor = cursor;
        }
        else if (showSystemPointer)
        {
            _cursorLayer.ShowSystemCursor();
        }

        // The anchor is the mouse, or the focus/caret while the user works with the keyboard (M6).
        // Frozen: the anchor of the moment of freezing; mouse, focus, caret and placements wait. It is also
        // where a pipeline rebuilt meanwhile goes on from after the thaw (instead of the monitor's middle).
        var anchor = frozen is not null
            ? StartAt(frozen.AnchorOr(_lastAnchor))
            : _anchors.Next(new AnchorInput(
                Stopwatch.GetTimestamp(), hasCursor ? cursor : _lastCursor, _cursorInside, _monitor.Bounds,
                request.AnchorMode, request.Zoom, request.Lens, request.Dock, request.Tracking, request.TrackingOptions, request.Placement));
        _lastAnchor = anchor;

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
            // A still image keeps the window regions it was taken with (and whether they filter here).
            var windows = _frozen.Windows(request.Windows);
            var windowsFiltered = ReferenceEquals(windows, request.Windows) ? request.WindowsFiltered : FiltersWindows(windows, request.Effects);
            weights = _desktop.Draw(_gpu.Context, image, _swapchain.RenderTarget, _swapchain.Width, _swapchain.Height,
                _monitor.Bounds, frame, request.Effects, windows, windowsFiltered, newImage);
        }

        // Focus frame and caret marker pause on a frozen monitor; the pointer cannot be there.
        if (frozen is null)
        {
            _highlights.Draw(_gpu.Context, _swapchain.RenderTarget, _monitor.Bounds, frame, request.Tracking, request.TrackingOptions);
        }

        _overview.Draw(_gpu.Context, _swapchain.RenderTarget, _monitor.Bounds, frame);
        _frozen.Draw(_gpu.Context, _swapchain.RenderTarget, _monitor.Bounds, frozen);
        LocatorActive = _cursorInside && _cursorLayer.Draw(
            _gpu.Context, _swapchain.RenderTarget, _monitor.Bounds, frame, request.Mode, request.Zoom, _lastCursor,
            request.Effects, request.LocateRequestedAt, request.PointerHidden);
        _snapshots.TakeIfRequested(_gpu.Device, _gpu.Context, request.OverlayOnly ? null : image,
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

    private PhysicalPoint StartAt(PhysicalPoint anchor)
    {
        _anchors.StartAt(anchor);
        return anchor;
    }

    private void Present()
    {
        _swapchain.Present();
        _holdsFrameSlot = false;
    }
}
