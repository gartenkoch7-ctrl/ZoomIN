using System.Diagnostics;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Capture;
using ZoomIN.Rendering.Freeze;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// Where the pointer and the view's anchor are on one monitor, frame by frame: the mouse, or the
/// focus/caret while the user works with the keyboard (M6); a frozen monitor keeps the anchor of the
/// moment of freezing (G2). Owned by one <see cref="RenderSession"/> on its render thread; 0 B per frame.
/// </summary>
internal sealed class SessionPointer
{
    private readonly PointerServices _pointer;
    private readonly PhysicalRect _bounds;
    private readonly CursorLayer _cursorLayer;
    private readonly FrozenLayer _frozen;
    private readonly AnchorController _anchors = new();
    private PhysicalPoint _lastAnchor;

    public SessionPointer(PointerServices pointer, MonitorOutput monitor, CursorLayer cursorLayer, FrozenLayer frozen)
    {
        (_pointer, _bounds, _cursorLayer, _frozen) = (pointer, monitor.Bounds, cursorLayer, frozen);
        LastCursor = new PhysicalPoint(_bounds.X + (monitor.Width / 2), _bounds.Y + (monitor.Height / 2));
        _lastAnchor = LastCursor;
    }

    /// <summary>The pointer is on this live monitor; it is drawn here.</summary>
    public bool Inside { get; private set; }

    /// <summary>Where the pointer was last seen on this monitor (the middle before that).</summary>
    public PhysicalPoint LastCursor { get; private set; }

    /// <summary>The pointer as read this frame, or <see cref="LastCursor"/> if it could not be read.</summary>
    public PhysicalPoint Seen { get; private set; }

    /// <summary>
    /// Reads the cursor (as late as possible: it decides what is shown under the pointer) and returns the
    /// anchor of this frame. A monitor without the mouse keeps its last view (M3 decision); a frozen one
    /// never draws it.
    /// </summary>
    public PhysicalPoint Track(in FrameRequest request)
    {
        var frozen = request.Frozen;
        var hasCursor = _pointer.Position.TryGetPosition(out var cursor);
        var onMonitor = hasCursor && _bounds.Contains(cursor);
        var showSystemPointer = _frozen.NeedsSystemPointer(Inside, onMonitor);
        Inside = onMonitor && frozen is null;
        if (Inside)
        {
            LastCursor = cursor;
        }
        else if (showSystemPointer)
        {
            _cursorLayer.ShowSystemCursor();
        }

        Seen = hasCursor ? cursor : LastCursor;

        // Frozen: the anchor of the moment of freezing; mouse, focus, caret and placements wait. It is also
        // where a pipeline rebuilt meanwhile goes on from after the thaw (instead of the monitor's middle).
        _lastAnchor = frozen is not null
            ? _anchors.StartAt(frozen.AnchorOr(_lastAnchor))
            : _anchors.Next(new AnchorInput(
                Stopwatch.GetTimestamp(), Seen, Inside, _bounds,
                request.AnchorMode, request.Zoom, request.Lens, request.Dock, request.Tracking, request.TrackingOptions, request.Placement));
        return _lastAnchor;
    }
}
