namespace ZoomIN.Core.Viewport;

/// <summary>
/// Computes which part of the desktop is shown magnified. The cursor is the fixpoint:
/// displayed(x) = p + z·(x − p), so the pixel under the real cursor is always shown exactly
/// where the cursor is and clicks hit what the user sees (ADR-0003).
/// </summary>
public static class ViewportCalculator
{
    public const double MinZoom = 1.0;

    /// <summary>
    /// Returns the source rectangle (desktop coordinates) that fills the monitor at the given zoom.
    /// The result always lies inside <paramref name="monitor"/>.
    /// </summary>
    public static PhysicalRect SourceRect(PhysicalRect monitor, PhysicalPoint cursor, double zoom)
    {
        var z = Math.Max(zoom, MinZoom);

        // Cursor relative to the monitor, clamped so a cursor on another monitor cannot push
        // the source rectangle outside this one.
        var px = Math.Clamp(cursor.X - monitor.X, 0, monitor.Width);
        var py = Math.Clamp(cursor.Y - monitor.Y, 0, monitor.Height);

        // Solving displayed(x) = 0 for x gives the left edge: p − p/z.
        var keep = 1.0 - (1.0 / z);
        return new PhysicalRect(
            monitor.X + (px * keep),
            monitor.Y + (py * keep),
            monitor.Width / z,
            monitor.Height / z);
    }
}
