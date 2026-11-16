using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tracking;

/// <summary>
/// Where the parts of the caret marker go (E2). Pure geometry on screen pixels, called per frame by
/// the render thread: no allocations.
/// </summary>
public static class CaretMarkerGeometry
{
    /// <summary>The line stops growing at this zoom, like the pointer (at most 8×).</summary>
    public const double MaxGrowth = 8;

    // A caret at least this wide relative to its height already spans the character (overwrite
    // mode, some UIA providers); a thinner one is a line in front of it.
    private const double WideCaret = 0.3;

    // Without a character width the frame assumes an average letter (plan: height × 0.5).
    private const double EstimatedCharWidth = 0.5;

    /// <summary>
    /// The caret to mark, if a marker is drawn at all: switched on and a real caret. An estimated
    /// caret (the focused element or a whole cell stands in for it) is followed but never marked.
    /// </summary>
    public static bool TryGetCaret(TrackingSnapshot tracking, TrackingOptions options, out PhysicalRect caret)
    {
        ArgumentNullException.ThrowIfNull(tracking);
        ArgumentNullException.ThrowIfNull(options);
        caret = tracking.Caret ?? default;
        return options.CaretMarker && !tracking.CaretEstimated && tracking.Caret is not null;
    }

    /// <summary>
    /// The marker is drawn only while the middle of the shown caret lies in the visible area. A caret
    /// just past a lens or strip edge gets none: moved inside, it would point at other letters; left
    /// in place, it would cover the unmagnified desktop next to the lens.
    /// </summary>
    public static bool InView(PhysicalRect caret, PhysicalRect area) =>
        area.Contains(new PhysicalPoint(caret.X + (caret.Width / 2), caret.Y + (caret.Height / 2)));

    /// <summary>Line strength in screen pixels: base × zoom (capped at <see cref="MaxGrowth"/>), never below a minimum.</summary>
    public static double LinePixels(CaretMarkerThickness thickness, double zoom)
    {
        var growth = Math.Clamp(zoom, 1, MaxGrowth);
        return thickness switch
        {
            CaretMarkerThickness.Thin => Math.Max(2, 0.5 * growth),
            CaretMarkerThickness.Thick => Math.Max(8, 2 * growth),

            // Medium: at least 4 px and one pixel per zoom step – the bar as it looked before E2.
            _ => Math.Max(4, growth),
        };
    }

    /// <param name="caret">The caret as shown on screen (already magnified).</param>
    /// <param name="zoom">Actual magnification of the view.</param>
    /// <param name="shape">Shape to draw.</param>
    /// <param name="thickness">Line strength.</param>
    /// <param name="area">Visible area (monitor, lens or strip); every part is moved inside it.</param>
    public static CaretMarkerParts Compute(
        PhysicalRect caret, double zoom, CaretMarkerShape shape, CaretMarkerThickness thickness, PhysicalRect area)
    {
        var line = LinePixels(thickness, zoom);
        var centerX = caret.X + (caret.Width / 2);
        switch (shape)
        {
            case CaretMarkerShape.Wedges:
            {
                // Grows with the caret and with the chosen thickness, so Thin/Medium/Thick differ at every zoom.
                var size = (2 * line) + (0.2 * caret.Height);
                var above = new PhysicalRect(centerX - size, caret.Y - size, 2 * size, size);
                var below = new PhysicalRect(centerX - size, caret.Bottom, 2 * size, size);
                return new(shape, Inside(above, area), Inside(below, area), line);
            }

            case CaretMarkerShape.Frame:
            {
                var wide = caret.Width >= caret.Height * WideCaret;
                var left = wide ? caret.X : centerX;
                var width = wide ? caret.Width : caret.Height * EstimatedCharWidth;
                var pad = 1.5 * line;
                var frame = new PhysicalRect(left - pad, caret.Y - pad, width + (2 * pad), caret.Height + (2 * pad));
                return new(shape, Inside(frame, area), default, line);
            }

            case CaretMarkerShape.Circle:
            {
                var radius = (caret.Height / 2) + (2 * line);
                var centerY = caret.Y + (caret.Height / 2);
                var ring = new PhysicalRect(centerX - radius, centerY - radius, 2 * radius, 2 * radius);
                return new(shape, Inside(ring, area), default, line);
            }

            default:
            {
                // Medium/Thick cover at least the magnified caret (the bar before E2); Thin may be
                // slimmer, otherwise it would equal Medium from 4× on with a 1 px caret.
                var width = thickness == CaretMarkerThickness.Thin ? line : Math.Max(line, caret.Width);
                var bar = new PhysicalRect(centerX - (width / 2), caret.Y, width, caret.Height);
                return new(CaretMarkerShape.Bar, Inside(bar, area), default, line);
            }
        }
    }

    // Moves a part into the visible area without resizing it, so it stays whole at the edge
    // (plan risk: wedges above a caret in the first line). Larger than the area: top-left aligned.
    private static PhysicalRect Inside(PhysicalRect r, PhysicalRect area) => r with
    {
        X = Math.Max(area.X, Math.Min(r.X, area.Right - r.Width)),
        Y = Math.Max(area.Y, Math.Min(r.Y, area.Bottom - r.Height)),
    };
}
