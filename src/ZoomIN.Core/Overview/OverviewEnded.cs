namespace ZoomIN.Core.Overview;

/// <summary>An overview ended (B2).</summary>
/// <param name="Session">The overview that ended.</param>
/// <param name="End">Zoom in, Esc or cancelled.</param>
/// <param name="Held">The shortcut was held (released after <see cref="OverviewTiming.TapLimit"/>), not tapped.</param>
public readonly record struct OverviewEnded(OverviewSession Session, OverviewEnd End, bool Held);
