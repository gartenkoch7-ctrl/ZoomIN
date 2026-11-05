using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tracking;

/// <summary>
/// Latest keyboard focus and text caret, published by the tracking thread and read per frame.
/// Timestamps are <see cref="System.Diagnostics.Stopwatch"/> ticks of the last change; 0 = never.
/// <paramref name="CaretEstimated"/>: no application reported a caret, so the focused element
/// (e.g. a grid cell) stands in for it. The view follows it like a caret, but no caret bar is drawn.
/// </summary>
public sealed record TrackingSnapshot(
    PhysicalRect? Focus, long FocusChangedAt, PhysicalRect? Caret, long CaretChangedAt, bool CaretEstimated = false)
{
    public static TrackingSnapshot Empty { get; } = new(null, 0, null, 0);
}
