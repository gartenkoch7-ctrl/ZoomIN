using ZoomIN.Core.Monitors;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Islands;

/// <summary>
/// Which island a point of the edit mode hits (M19 B1). Pure: the topmost island the user sees at the
/// point, whose picture contains it or (with corners) has a corner within <see cref="HandleReach"/>.
/// Islands that are not drawn (monitor gone, picture outside it) cannot be hit, and a higher island's
/// reach never hides a lower island under the point.
/// </summary>
public static class IslandGrab
{
    /// <summary>How far outside or inside a corner a press still grabs it, in pixels.</summary>
    public const double HandleReach = 12;

    /// <returns>Index in <paramref name="set"/>, or −1.</returns>
    public static int Find(
        IslandSet set, IMonitorProvider monitors, PhysicalPoint point, bool withCorners, out IslandCorner? corner, out PhysicalRect monitor)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(monitors);
        for (var i = set.Count - 1; i >= 0; i--)
        {
            if (!IsShown(set[i], monitors, out monitor))
            {
                continue;
            }

            var target = set[i].Target;
            corner = withCorners ? CornerAt(target, point) : null;
            if (corner is not null || target.Contains(point))
            {
                return i;
            }
        }

        corner = null;
        monitor = default;
        return -1;
    }

    /// <summary>As the render threads decide (IslandLayer): drawn only while its monitor exists and the picture fits on it.</summary>
    public static bool IsShown(Island island, IMonitorProvider monitors, out PhysicalRect monitor)
    {
        ArgumentNullException.ThrowIfNull(island);
        ArgumentNullException.ThrowIfNull(monitors);
        monitor = monitors.BoundsOf(island.MonitorId) ?? default;
        return IslandRules.Fits(island, monitor);
    }

    /// <summary>A corner of the picture within reach of the point, or null.</summary>
    public static IslandCorner? CornerAt(PhysicalRect target, PhysicalPoint point)
    {
        var left = Math.Abs(point.X - target.X) <= HandleReach;
        var right = Math.Abs(point.X - target.Right) <= HandleReach;
        var top = Math.Abs(point.Y - target.Y) <= HandleReach;
        var bottom = Math.Abs(point.Y - target.Bottom) <= HandleReach;
        return (left, right, top, bottom) switch
        {
            (true, _, true, _) => IslandCorner.TopLeft,
            (_, true, true, _) => IslandCorner.TopRight,
            (true, _, _, true) => IslandCorner.BottomLeft,
            (_, true, _, true) => IslandCorner.BottomRight,
            _ => null,
        };
    }
}
