using ZoomIN.Core.Modes;
using ZoomIN.Core.Viewport;
using static ZoomIN.Core.Tracking.AnchorGeometry;

namespace ZoomIN.Core.Tracking;

/// <summary>
/// Where the view is anchored while it follows the mouse, per <see cref="MouseFollowMode"/> (M17 A1).
/// Pure per-frame math for <see cref="AnchorController"/>: no allocations. In every mode the
/// pointer is drawn where its desktop point is shown, so clicks hit (ADR-0003, ADR-0018).
/// </summary>
internal static class MouseFollowTarget
{
    // A push this small is rounding in the geometry round trip, not a mouse step: moving the view
    // by it every frame would keep the renderer from ever idling at the margin.
    private const double Tolerance = 1e-6;

    /// <param name="heading">Where the view is now or, while gliding, where it is heading.</param>
    /// <param name="previousZoom">Zoom of the previous frame; a change keeps the pointer where it is on screen
    /// (pass the current zoom to skip that). Called only while the mouse is on this monitor.</param>
    public static PhysicalPoint For(in AnchorInput input, PhysicalPoint heading, double previousZoom)
    {
        // Docked shows the area around the mouse in its strip; nothing to choose there.
        if (input.Mode == ZoomMode.Docked)
        {
            return input.Mouse;
        }

        return input.Options.MouseFollow switch
        {
            MouseFollowMode.Center => ViewCentering.AnchorFor(input.Mode, input.Monitor, input.Mouse, input.Zoom, input.Lens, input.Dock),
            MouseFollowMode.Fixpoint => input.Mouse,
            // The lens window is centred on the anchor: re-anchoring would slide it while zooming,
            // so it stays put and the margin keeps the pointer in it.
            _ => Margin(input, input.Zoom == previousZoom || input.Mode == ZoomMode.Lens ? heading : KeepPointer(input, heading, previousZoom)),
        };
    }

    /// <summary>
    /// The view stays while the pointer is inside the margin; beyond it, it is pushed just far
    /// enough that the pointer sits exactly on the margin – like the caret, no delay, no jump.
    /// At the monitor edge the view stops (clamped) and the pointer moves on into the margin.
    /// </summary>
    private static PhysicalPoint Margin(in AnchorInput input, PhysicalPoint heading)
    {
        var margin = input.Options.MouseMargin;
        margin = double.IsFinite(margin) ? Math.Clamp(margin, 0, TrackingOptions.MaxMouseMargin) : TrackingOptions.Default.MouseMargin;
        var visible = ModeGeometry.Compute(input.Mode, input.Monitor, heading, input.Zoom, input.Lens, input.Dock).Source;
        var inner = Shrink(visible, margin);
        var dx = PointPush(input.Mouse.X, inner.X, inner.Right);
        var dy = PointPush(input.Mouse.Y, inner.Y, inner.Bottom);
        if (dx == 0 && dy == 0)
        {
            return heading;
        }

        var middle = Center(visible);
        var center = new PhysicalPoint(middle.X + dx, middle.Y + dy);
        var moved = ViewCentering.AnchorFor(input.Mode, input.Monitor, center, input.Zoom, input.Lens, input.Dock);

        // Per axis (the geometry is separable): an axis without a push keeps its exact value.
        return new PhysicalPoint(dx == 0 ? heading.X : moved.X, dy == 0 ? heading.Y : moved.Y);
    }

    // The caret's push for a point, with rounding taken as no push.
    private static double PointPush(double p, double low, double high)
    {
        var push = Push(p, p, low, high);
        return Math.Abs(push) <= Tolerance ? 0 : push;
    }

    /// <summary>
    /// Fullscreen: a zoom change keeps the pointer at its place on screen, so the content under it
    /// does not slide away. Fullscreen (and lens) show a desktop point x at p + z·(x − p) (fixpoint p), so
    /// the anchor that keeps the mouse m at its screen point s is (z·m − s) / (z − 1). From 1×
    /// this is the mouse itself: zooming in starts at the pointer, as with the fixpoint.
    /// </summary>
    private static PhysicalPoint KeepPointer(in AnchorInput input, PhysicalPoint heading, double previousZoom)
    {
        var from = Math.Max(previousZoom, ViewportCalculator.MinZoom);
        var to = Math.Max(input.Zoom, ViewportCalculator.MinZoom);
        return new PhysicalPoint(
            Keep(heading.X, input.Mouse.X, from, to, input.Monitor.X, input.Monitor.Right),
            Keep(heading.Y, input.Mouse.Y, from, to, input.Monitor.Y, input.Monitor.Bottom));
    }

    private static double Keep(double p, double m, double from, double to, double min, double max)
    {
        // At 1× every anchor shows the same; the mouse is where the next zoom-in should start.
        if (to - 1 <= 1e-9)
        {
            return Math.Clamp(m, min, max);
        }

        var shown = p + (from * (m - p));
        return Math.Clamp(((to * m) - shown) / (to - 1), min, max);
    }
}
