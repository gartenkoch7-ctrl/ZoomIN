namespace ZoomIN.Core.Tracking;

/// <summary>
/// What the user did last, read once per frame (E1). Timestamps are <see cref="TimeProvider"/>
/// ticks (the system clock = <see cref="System.Diagnostics.Stopwatch"/> ticks).
/// </summary>
/// <param name="LastActivity">Last mouse movement or typed key; 0 = none yet.</param>
/// <param name="HoldSince">Hold key down since; 0 = not held, −1 = held while typing (does not count).</param>
/// <param name="HoldSeen">Last "down" of the hold key, key repeat included; 0 = never.</param>
public readonly record struct MarkerActivitySample(long LastActivity, long HoldSince, long HoldSeen = 0);
