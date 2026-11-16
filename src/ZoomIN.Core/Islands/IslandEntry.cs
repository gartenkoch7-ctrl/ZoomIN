using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Islands;

/// <summary>
/// One saved island (M19 B1): monitor, source rectangle, place of the picture, level, live or
/// still. Only rectangles and numbers, no content. Properties use <c>set</c> for the JSON source
/// generator (see the remark on AppSettings).
/// </summary>
public sealed record IslandEntry
{
    public string MonitorId { get; set; } = string.Empty;

    public double SourceX { get; set; }

    public double SourceY { get; set; }

    public double SourceWidth { get; set; }

    public double SourceHeight { get; set; }

    public double TargetX { get; set; }

    public double TargetY { get; set; }

    public double Level { get; set; } = 2;

    /// <summary>A still image, taken again after each start (the image itself is never saved).</summary>
    public bool Still { get; set; }

    public static IslandEntry From(Island island)
    {
        ArgumentNullException.ThrowIfNull(island);
        return new IslandEntry
        {
            MonitorId = island.MonitorId,
            SourceX = island.Source.X,
            SourceY = island.Source.Y,
            SourceWidth = island.Source.Width,
            SourceHeight = island.Source.Height,
            TargetX = island.TargetOrigin.X,
            TargetY = island.TargetOrigin.Y,
            Level = island.Level,
            Still = island.Still,
        };
    }

    /// <summary>The island described; <see cref="IslandSet.From"/> numbers it and fits it to its monitor.</summary>
    public Island ToIsland() => new(
        0, MonitorId ?? string.Empty, new PhysicalRect(SourceX, SourceY, SourceWidth, SourceHeight),
        new PhysicalPoint(TargetX, TargetY), Level, Still);
}
