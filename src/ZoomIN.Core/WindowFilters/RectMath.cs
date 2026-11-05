using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.WindowFilters;

/// <summary>Overlap tests for window rectangles; no allocation (also used by render threads).</summary>
public static class RectMath
{
    public static bool Intersects(PhysicalRect a, PhysicalRect b) =>
        a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;

    /// <summary><paramref name="outer"/> covers all of <paramref name="inner"/>.</summary>
    public static bool Covers(PhysicalRect outer, PhysicalRect inner) =>
        outer.X <= inner.X && outer.Y <= inner.Y && outer.Right >= inner.Right && outer.Bottom >= inner.Bottom;

    /// <summary>The common part; empty (width or height 0) if they do not overlap.</summary>
    public static PhysicalRect Intersection(PhysicalRect a, PhysicalRect b)
    {
        var x = Math.Max(a.X, b.X);
        var y = Math.Max(a.Y, b.Y);
        var right = Math.Min(a.Right, b.Right);
        var bottom = Math.Min(a.Bottom, b.Bottom);
        return right > x && bottom > y ? new PhysicalRect(x, y, right - x, bottom - y) : new PhysicalRect(x, y, 0, 0);
    }

    public static bool IsEmpty(PhysicalRect rect) => rect.Width <= 0 || rect.Height <= 0;
}
