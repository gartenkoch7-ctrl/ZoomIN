using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Islands;

/// <summary>
/// One island (M19 B1): a fixed part of a monitor (<see cref="Source"/>) shown magnified by
/// <see cref="Level"/> at a fixed place on the same monitor (<see cref="Target"/>), above the
/// zoom. Both rectangles are physical desktop pixels. Immutable; <see cref="IslandRules.Fit"/>
/// keeps it inside its monitor.
/// </summary>
/// <param name="Id">Identifies the island while ZoomIN runs (its still image belongs to it); not saved.</param>
/// <param name="MonitorId">Device name of the monitor, e.g. "\\.\DISPLAY1".</param>
/// <param name="Source">What is shown, in desktop pixels of that monitor.</param>
/// <param name="TargetOrigin">Top-left corner of the magnified picture.</param>
/// <param name="Level">Magnification, within <see cref="Zoom.ZoomLimits"/>.</param>
/// <param name="Still">Shows a still image taken once instead of the live picture.</param>
public sealed record Island(int Id, string MonitorId, PhysicalRect Source, PhysicalPoint TargetOrigin, double Level, bool Still = false)
{
    /// <summary>Where the magnified picture is drawn: the source size times the level.</summary>
    public PhysicalRect Target => new(TargetOrigin.X, TargetOrigin.Y, Source.Width * Level, Source.Height * Level);
}
