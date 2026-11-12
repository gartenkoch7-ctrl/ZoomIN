using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Islands;

/// <summary>
/// Resizing an island by a corner (M19 B1): the level stays, the picture and with it the source
/// grow or shrink; the opposite corner of both stays where it is, so the island shows more or
/// less of its surroundings on that side. Pure; <see cref="IslandRules.Fit"/> runs afterwards.
/// </summary>
public static class IslandResize
{
    public static Island ToCorner(Island island, IslandCorner corner, PhysicalPoint point)
    {
        ArgumentNullException.ThrowIfNull(island);
        var level = island.Level;
        var target = island.Target;
        var source = island.Source;
        var minimum = IslandRules.MinSize * level;
        var left = corner is IslandCorner.TopLeft or IslandCorner.BottomLeft;
        var top = corner is IslandCorner.TopLeft or IslandCorner.TopRight;

        // The far edge never passes the fixed one: below the minimum size the drag stops.
        var width = Math.Max(left ? target.Right - point.X : point.X - target.X, minimum);
        var height = Math.Max(top ? target.Bottom - point.Y : point.Y - target.Y, minimum);
        var sourceWidth = width / level;
        var sourceHeight = height / level;
        var newSource = new PhysicalRect(
            left ? source.Right - sourceWidth : source.X,
            top ? source.Bottom - sourceHeight : source.Y,
            sourceWidth,
            sourceHeight);
        var origin = new PhysicalPoint(left ? target.Right - width : target.X, top ? target.Bottom - height : target.Y);
        return island with { Source = newSource, TargetOrigin = origin };
    }
}
