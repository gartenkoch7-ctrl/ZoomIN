using ZoomIN.Core.Monitors;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Islands;

/// <summary>
/// Which island a point of the edit mode hits (M19 B1). Pure: the topmost island the user sees under
/// the point, else (with corners) the topmost one with a corner within <see cref="HandleReach"/>.
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

        // First the island under the point (topmost), with its corner if one is in reach; only beside
        // every island does a corner's reach outside a picture count.
        for (var pass = 0; pass < (withCorners ? 2 : 1); pass++)
        {
            for (var i = set.Count - 1; i >= 0; i--)
            {
                var target = set[i].Target;
                var hit = pass == 0 ? target.Contains(point) : CornerAt(target, point) is not null;
                if (hit && IsShown(set[i], monitors, out monitor))
                {
                    corner = withCorners ? CornerAt(target, point) : null;
                    return i;
                }
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

    /// <summary>
    /// A corner of the picture within reach of the point, or null. The reach shrinks to a quarter of
    /// a small picture's side, so its middle still moves it instead of always resizing.
    /// </summary>
    public static IslandCorner? CornerAt(PhysicalRect target, PhysicalPoint point)
    {
        var reach = Math.Min(HandleReach, Math.Min(target.Width, target.Height) / 4);
        var left = Math.Abs(point.X - target.X) <= reach;
        var right = Math.Abs(point.X - target.Right) <= reach;
        var top = Math.Abs(point.Y - target.Y) <= reach;
        var bottom = Math.Abs(point.Y - target.Bottom) <= reach;
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
