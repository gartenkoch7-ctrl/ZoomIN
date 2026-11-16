using ZoomIN.Core.Modes;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Overview;

/// <summary>
/// The frame of the overview (B2): the part of the desktop the user's level would show, starting
/// as the previous view and moved by the mouse, kept inside the monitor. Zooming in centres the
/// view on <see cref="Center"/>, so after the glide the frame's content fills the view. Pure math,
/// called per frame by the render thread: no allocation.
/// </summary>
public static class OverviewFrame
{
    /// <param name="session">The running overview (its mode and recorded start).</param>
    /// <param name="monitor">The overview's monitor.</param>
    /// <param name="mouse">Mouse now; outside the monitor the frame stops at the edge.</param>
    /// <param name="level">The user's level now: a wheel step during the overview resizes the frame.</param>
    /// <param name="follow">
    /// How the view will follow the mouse after zooming in. Only the margin mode takes the view to
    /// the frame (view placement); with Center or Fixpoint the frame shows what the mouse will get.
    /// </param>
    public static PhysicalRect Rect(
        OverviewSession session, PhysicalRect monitor, PhysicalPoint mouse, double level, LensSize lens, DockLayout dock,
        MouseFollowMode follow = MouseFollowMode.Margin)
    {
        ArgumentNullException.ThrowIfNull(session);
        var view = ModeGeometry.Compute(session.Mode, monitor, mouse, level, lens, dock).Source;
        if (follow == MouseFollowMode.Fixpoint)
        {
            return view; // the fixpoint view around the mouse, exactly as it will be
        }

        var size = view;

        // Relative to the start, so the frame first shows the previous view even where the view
        // was not centred on the mouse (fixpoint, margin). The offset is at most half the frame:
        // the mouse stays inside it, so every part of the monitor can be framed (focus elsewhere).
        var center = mouse;
        if (follow == MouseFollowMode.Margin && session.TryGetStart(out var before, out var start))
        {
            var viewCenter = Center(before);
            center = new PhysicalPoint(
                mouse.X + Math.Clamp(viewCenter.X - start.X, -size.Width / 2, size.Width / 2),
                mouse.Y + Math.Clamp(viewCenter.Y - start.Y, -size.Height / 2, size.Height / 2));
        }

        return new PhysicalRect(
            Place(center.X, size.Width, monitor.X, monitor.Right),
            Place(center.Y, size.Height, monitor.Y, monitor.Bottom),
            Math.Min(size.Width, monitor.Width),
            Math.Min(size.Height, monitor.Height));
    }

    public static PhysicalPoint Center(PhysicalRect rect) => new(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));

    private static double Place(double center, double size, double min, double max) =>
        size >= max - min ? min : Math.Clamp(center - (size / 2), min, max - size);
}
