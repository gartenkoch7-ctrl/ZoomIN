using Vortice.Direct3D11;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// The overview frame (B2) in the colour of the lens frame, drawn on the overlay like the focus
/// frame. Also records the view shown before an overview, which the frame starts from and Esc
/// returns to. Every frame on the render thread: no allocation.
/// </summary>
internal sealed class OverviewLayer
{
    // As thick as the lens frame, drawn inside the rectangle so it stays visible at the monitor edge.
    private const double FrameWidth = 4;

    private readonly CursorPass _pass;
    private PhysicalRect _lastView;
    private bool _hasView;
    private PhysicalRect _rect;
    private bool _hasRect;
    private PhysicalRect _drawnRect;
    private bool _drawnHasRect;

    public OverviewLayer(CursorPass pass)
    {
        _pass = pass;
    }

    /// <summary>
    /// The frame moved, appeared or went away since it was last drawn (also while the mouse is on
    /// another monitor, where the pointer does not trigger a redraw of this one).
    /// </summary>
    public bool NeedsRedraw => _hasRect != _drawnHasRect || (_hasRect && _rect != _drawnRect);

    /// <summary>
    /// Call every frame before <see cref="Draw"/>. Without an overview it remembers the view; in
    /// the first frame of one it hands the view of the frame before to the overview.
    /// </summary>
    /// <param name="level">The user's level: the frame shows what zooming in would show.</param>
    /// <param name="viewHeld">A temporary level of another feature (window drag, G1) holds the monitor: the
    /// view from before it stays the one an overview starts from and Esc returns to (M17).</param>
    public void Update(in ModeFrame frame, OverviewSession? overview, PhysicalRect monitor, PhysicalPoint mouse,
        double level, LensSize lens, DockLayout dock, MouseFollowMode follow = MouseFollowMode.Margin, bool viewHeld = false)
    {
        if (overview is null)
        {
            if (!viewHeld)
            {
                _lastView = frame.Source;
                _hasView = true;
            }

            _hasRect = false;
            return;
        }

        // Only the first call per overview counts; the glide to 1× has already begun in this frame.
        overview.CaptureStart(_hasView ? _lastView : frame.Source, mouse);
        _rect = OverviewFrame.Rect(overview, monitor, mouse, level, lens, dock, follow);
        _hasRect = true;
    }

    /// <summary>The overlay was hidden (render loop asleep): the remembered view is out of date (not under a temporary 1×, see Update).</summary>
    public void Forget() => _hasView = false;

    public void Draw(ID3D11DeviceContext context, ID3D11RenderTargetView target, PhysicalRect monitor, in ModeFrame frame)
    {
        _drawnRect = _rect;
        _drawnHasRect = _hasRect;
        if (!_hasRect || !FrameMapping.Intersects(_rect, frame.Source))
        {
            return;
        }

        var r = FrameMapping.Map(frame, _rect);
        try
        {
            _pass.DrawRect(context, target, (float)(r.X - monitor.X), (float)(r.Y - monitor.Y), (float)r.Width, (float)r.Height,
                CursorConstants.ForRect((float)(FrameWidth / r.Width), (float)(FrameWidth / r.Height), FrameColor.Default));
        }
        finally
        {
            _pass.End(context);
        }
    }
}
