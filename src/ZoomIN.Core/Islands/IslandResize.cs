using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Islands;

/// <summary>
/// Resizing an island by a corner (M19 B1): the level stays, the picture and with it the source
/// grow or shrink; the opposite corner of both stays where it is, so the island shows more or
/// less of its surroundings on that side. At the monitor edge the drag stops (neither source nor
/// picture may leave the monitor, or <see cref="IslandRules.Fit"/> would move the fixed corner or
/// lower the level). Pure; <see cref="IslandRules.Fit"/> runs afterwards.
/// </summary>
public static class IslandResize
{
    /// <param name="corner">Where the dragged corner is now (the pointer plus where in the handle it was grabbed).</param>
    public static Island ToCorner(Island island, IslandCorner which, PhysicalPoint corner, PhysicalRect monitor)
    {
        ArgumentNullException.ThrowIfNull(island);
        var level = island.Level;
        var target = island.Target;
        var source = island.Source;
        var minimum = IslandRules.MinSize * level;
        var left = which is IslandCorner.TopLeft or IslandCorner.BottomLeft;
        var top = which is IslandCorner.TopLeft or IslandCorner.TopRight;

        // Room towards the dragged side, for the picture and (times the level) for the source.
        var roomX = Math.Min(left ? target.Right - monitor.X : monitor.Right - target.X, (left ? source.Right - monitor.X : monitor.Right - source.X) * level);
        var roomY = Math.Min(top ? target.Bottom - monitor.Y : monitor.Bottom - target.Y, (top ? source.Bottom - monitor.Y : monitor.Bottom - source.Y) * level);

        // The far edge never passes the fixed one: below the minimum size the drag stops. Whole source
        // pixels, so Fit's rounding cannot move the fixed edge (it would jitter by up to level / 2).
        var sourceWidth = WholePixels(left ? target.Right - corner.X : corner.X - target.X, roomX, minimum, level);
        var sourceHeight = WholePixels(top ? target.Bottom - corner.Y : corner.Y - target.Y, roomY, minimum, level);
        var width = sourceWidth * level;
        var height = sourceHeight * level;
        var newSource = new PhysicalRect(
            left ? source.Right - sourceWidth : source.X,
            top ? source.Bottom - sourceHeight : source.Y,
            sourceWidth,
            sourceHeight);
        var origin = new PhysicalPoint(left ? target.Right - width : target.X, top ? target.Bottom - height : target.Y);
        return island with { Source = newSource, TargetOrigin = origin };
    }

    private static double WholePixels(double picture, double room, double minimum, double level) =>
        Math.Max(Math.Min(Math.Round(picture / level), Math.Floor(room / level)), minimum / level);

    /// <summary>Where the corner of the picture is.</summary>
    public static PhysicalPoint CornerOf(PhysicalRect picture, IslandCorner which) => which switch
    {
        IslandCorner.TopLeft => new(picture.X, picture.Y),
        IslandCorner.TopRight => new(picture.Right, picture.Y),
        IslandCorner.BottomLeft => new(picture.X, picture.Bottom),
        _ => new(picture.Right, picture.Bottom),
    };
}
