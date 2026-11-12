using ZoomIN.Core.Modes;
using ZoomIN.Core.Overlay;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// Remembers what the last presented frame of one monitor showed and tells what changed since
/// (M14 step 3). Snapshots from the models are immutable, so a new reference means a change.
/// Runs every frame on the render thread: no allocations, no locks (AGENTS.md 2.3).
/// </summary>
internal sealed class FrameChangeTracker
{
    private FrameRequest _request;
    private ModeFrame _frame;
    private PhysicalPoint _cursor;
    private bool _cursorInside;
    private nint _shape;
    private bool _presented;

    /// <param name="newImage">Desktop Duplication delivered a new image this frame.</param>
    /// <param name="shape">Current pointer handle, 0 if none is shown.</param>
    /// <param name="animating">Locator ring or smart inversion smoothing still running.</param>
    /// <param name="snapshotRequested">The diagnostics page waits for a debug snapshot.</param>
    /// <param name="shown">The overlay is on screen.</param>
    public FrameChanges Compare(
        bool newImage, in FrameRequest request, in ModeFrame frame, bool cursorInside, PhysicalPoint cursor, nint shape,
        bool animating, bool snapshotRequested, bool shown)
    {
        // The pointer is drawn only on the monitor it is on; elsewhere its moves change nothing.
        var pointerChanged = cursorInside != _cursorInside || (cursorInside && (cursor != _cursor || shape != _shape));
        return new FrameChanges(
            newImage,
            pointerChanged,
            frame != _frame,
            !SameRequest(request, _request),
            animating,
            snapshotRequested,
            !_presented || !shown);
    }

    /// <summary>The frame described by these values is on screen now.</summary>
    public void Presented(in FrameRequest request, in ModeFrame frame, bool cursorInside, PhysicalPoint cursor, nint shape)
    {
        _request = request;
        _frame = frame;
        _cursorInside = cursorInside;
        _cursor = cursor;
        _shape = shape;
        _presented = true;
    }

    // By reference for the snapshots (immutable, a change is a new object), by value for the rest.
    private static bool SameRequest(in FrameRequest a, in FrameRequest b) =>
        a.Mode == b.Mode
        && a.Zoom == b.Zoom
        && a.Lens == b.Lens
        && a.Dock == b.Dock
        && ReferenceEquals(a.Effects, b.Effects)
        && a.LocateRequestedAt == b.LocateRequestedAt
        && ReferenceEquals(a.Tracking, b.Tracking)
        && ReferenceEquals(a.TrackingOptions, b.TrackingOptions)
        && a.PointerHidden == b.PointerHidden
        && ReferenceEquals(a.Windows, b.Windows)
        && a.WindowsFiltered == b.WindowsFiltered
        && ReferenceEquals(a.Frozen, b.Frozen);
}
