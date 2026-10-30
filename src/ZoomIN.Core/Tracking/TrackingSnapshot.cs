using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tracking;

/// <summary>
/// Latest keyboard focus and text caret, published by the tracking thread and read per frame.
/// Timestamps are <see cref="System.Diagnostics.Stopwatch"/> ticks of the last change; 0 = never.
/// </summary>
public sealed record TrackingSnapshot(PhysicalRect? Focus, long FocusChangedAt, PhysicalRect? Caret, long CaretChangedAt)
{
    public static TrackingSnapshot Empty { get; } = new(null, 0, null, 0);
}
