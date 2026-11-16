using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Monitors;

/// <summary>
/// Which monitor the pointer goes to (G2). The order follows the physical layout, not the Windows
/// number: monitors are ordered by their centre, x first, then y within the same column. Ctrl+Alt+N
/// walks back and forth (1 → 3 → 2 → 3 → 1 with 1 left, 3 middle, 2 right): at an end it turns, from
/// the middle it starts to the right, and it jumps straight to the next free neighbour, skipping
/// frozen monitors. Pure and 0 B: it finds the neighbour by comparing keys instead of sorting.
/// </summary>
/// <remarks>
/// Monitors are given as parallel spans: bounds and a frozen flag per index. "Same column" means the
/// same centre x, as with monitors of equal width stacked on top of each other.
/// </remarks>
public static class MonitorCycle
{
    /// <summary>The monitor after <paramref name="current"/> in <paramref name="direction"/>, turning at an end.</summary>
    /// <param name="current">Index of the monitor the pointer is on; outside the spans = unknown (then the first free one in order).</param>
    /// <param name="direction">Walking direction; turned when the walk hits an end. Start with <see cref="CycleDirection.Right"/>.</param>
    /// <param name="skipped">Frozen monitors jumped over between the two.</param>
    /// <returns>Index of the next monitor, or −1 if there is no other free monitor.</returns>
    public static int Next(ReadOnlySpan<PhysicalRect> monitors, ReadOnlySpan<bool> frozen, int current, ref CycleDirection direction, out int skipped)
    {
        Check(monitors, frozen);
        if ((uint)current >= (uint)monitors.Length)
        {
            return Closest(monitors, frozen, -1, CycleDirection.Right, out skipped);
        }

        for (var turn = 0; turn < 2; turn++)
        {
            var next = Closest(monitors, frozen, current, direction, out skipped);
            if (next >= 0)
            {
                return next;
            }

            // An end: walk back the other way (after two turns the direction is as it was).
            direction = direction == CycleDirection.Right ? CycleDirection.Left : CycleDirection.Right;
        }

        skipped = 0;
        return -1;
    }

    /// <summary>
    /// The free monitor closest to <paramref name="point"/> (distance to its rectangle), where the
    /// pointer jumps when its monitor is frozen. At equal distance the one further right wins
    /// ("rechts zuerst"), then the layout order.
    /// </summary>
    /// <returns>Index of that monitor, or −1 if none is free.</returns>
    public static int NearestFree(ReadOnlySpan<PhysicalRect> monitors, ReadOnlySpan<bool> frozen, PhysicalPoint point)
    {
        Check(monitors, frozen);
        var best = -1;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < monitors.Length; i++)
        {
            if (frozen[i])
            {
                continue;
            }

            var distance = Distance(monitors[i], point);
            if (best < 0 || distance < bestDistance
                || (distance == bestDistance && Compare(monitors, i, best) > 0))
            {
                best = i;
                bestDistance = distance;
            }
        }

        return best;
    }

    // The free monitor right after `from` in the direction; `from` = −1 means before all of them.
    private static int Closest(ReadOnlySpan<PhysicalRect> monitors, ReadOnlySpan<bool> frozen, int from, CycleDirection direction, out int skipped)
    {
        var best = -1;
        for (var i = 0; i < monitors.Length; i++)
        {
            if (i != from && !frozen[i] && After(monitors, i, from, direction) && (best < 0 || After(monitors, best, i, direction)))
            {
                best = i;
            }
        }

        skipped = 0;
        for (var i = 0; best >= 0 && i < monitors.Length; i++)
        {
            if (i != from && frozen[i] && After(monitors, i, from, direction) && After(monitors, best, i, direction))
            {
                skipped++;
            }
        }

        return best;
    }

    private static bool After(ReadOnlySpan<PhysicalRect> monitors, int a, int b, CycleDirection direction) =>
        b < 0 || (direction == CycleDirection.Right ? Compare(monitors, a, b) > 0 : Compare(monitors, a, b) < 0);

    // Layout order: centre x, then centre y, then the index (two monitors on the same spot, e.g. mirrored).
    private static int Compare(ReadOnlySpan<PhysicalRect> monitors, int a, int b)
    {
        var x = CenterX(monitors[a]).CompareTo(CenterX(monitors[b]));
        if (x != 0)
        {
            return x;
        }

        var y = CenterY(monitors[a]).CompareTo(CenterY(monitors[b]));
        return y != 0 ? y : a.CompareTo(b);
    }

    private static double CenterX(PhysicalRect r) => r.X + (r.Width / 2);

    private static double CenterY(PhysicalRect r) => r.Y + (r.Height / 2);

    // Squared distance to the closed rectangle: equal for both neighbours of a pointer in the exact middle.
    private static double Distance(PhysicalRect r, PhysicalPoint p)
    {
        var dx = Math.Max(Math.Max(r.X - p.X, 0), p.X - r.Right);
        var dy = Math.Max(Math.Max(r.Y - p.Y, 0), p.Y - r.Bottom);
        return (dx * dx) + (dy * dy);
    }

    private static void Check(ReadOnlySpan<PhysicalRect> monitors, ReadOnlySpan<bool> frozen)
    {
        if (monitors.Length != frozen.Length)
        {
            throw new ArgumentException("One frozen flag per monitor is needed.", nameof(frozen));
        }
    }
}
