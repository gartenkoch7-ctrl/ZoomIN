using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tracking;

/// <summary>Rectangle and point arithmetic for <see cref="AnchorController"/>; pure, no allocations.</summary>
internal static class AnchorGeometry
{
    // Focus rectangles covering more of the monitor (whole windows/documents) say nothing useful.
    private const double MaxFocusShare = 0.5;

    public static bool Usable(PhysicalRect focus, PhysicalRect monitor) =>
        monitor.Contains(Center(focus)) && focus.Width * focus.Height <= monitor.Width * monitor.Height * MaxFocusShare;

    public static bool Inside(PhysicalRect inner, PhysicalRect outer) =>
        inner.X >= outer.X && inner.Right <= outer.Right && inner.Y >= outer.Y && inner.Bottom <= outer.Bottom;

    private static bool Overlaps(PhysicalRect a, PhysicalRect b) =>
        a.X < b.Right && a.Right > b.X && a.Y < b.Bottom && a.Bottom > b.Y;

    /// <summary>
    /// Where the view centres for a caret outside the inner area. Out of sight (new line, other
    /// field): on the caret. Within the margin: pushed just far enough that the caret sits on the
    /// margin, so typing scrolls evenly instead of jumping back to the middle every few characters.
    /// </summary>
    public static PhysicalPoint CaretCenter(PhysicalRect caret, PhysicalRect visible, PhysicalRect inner) =>
        Overlaps(caret, visible)
            ? new PhysicalPoint(
                visible.X + (visible.Width / 2) + Push(caret.X, caret.Right, inner.X, inner.Right),
                visible.Y + (visible.Height / 2) + Push(caret.Y, caret.Bottom, inner.Y, inner.Bottom))
            : Center(caret);

    // Smallest shift that brings [start, end] into [low, high]; centred if it does not fit.
    private static double Push(double start, double end, double low, double high)
    {
        if (end - start > high - low)
        {
            return ((start + end) / 2) - ((low + high) / 2);
        }

        return start < low ? start - low : end > high ? end - high : 0;
    }

    public static PhysicalRect Shrink(PhysicalRect r, double margin) =>
        new(r.X + (r.Width * margin), r.Y + (r.Height * margin), r.Width * (1 - (2 * margin)), r.Height * (1 - (2 * margin)));

    public static PhysicalPoint Center(PhysicalRect r) => new(r.X + (r.Width / 2), r.Y + (r.Height / 2));

    public static double Distance(PhysicalPoint a, PhysicalPoint b) =>
        Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));
}
