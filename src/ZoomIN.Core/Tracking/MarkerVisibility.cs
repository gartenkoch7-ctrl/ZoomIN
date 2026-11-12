using ZoomIN.Core.Pointer;

namespace ZoomIN.Core.Tracking;

/// <summary>Whether a highlight is shown under its <see cref="ShowCondition"/> (E1). Pure, no allocation.</summary>
public static class MarkerVisibility
{
    /// <summary>"At rest" = no mouse movement and no typing for this long (E1 decision).</summary>
    public static readonly TimeSpan RestTime = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The hold key counts only after this long, so Shift for a capital letter does not flash the
    /// highlight (plan risk: right Shift while typing).
    /// </summary>
    public static readonly TimeSpan MinHoldTime = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// While held, the hook confirms the key every 400 ms (real key state) and key repeat adds more.
    /// Without any sign for this long the hook is gone (removed by Windows, stopped) and the hold no
    /// longer counts.
    /// </summary>
    public static readonly TimeSpan HoldTimeout = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// The global keyboard hook is needed only while a switched-on highlight waits for the hold key;
    /// a highlight that is off draws nothing (and <see cref="MarkerAlphas"/> keeps it hidden).
    /// </summary>
    public static bool NeedsHoldKey(PointerStyle cursor, TrackingOptions tracking)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        ArgumentNullException.ThrowIfNull(tracking);
        return (cursor.CrosshairEnabled && cursor.CrosshairShow == ShowCondition.HoldKey)
            || (tracking.Enabled && tracking.CaretMarker && tracking.CaretShow == ShowCondition.HoldKey)
            || (tracking.Enabled && tracking.FocusFrame && tracking.FocusFrameShow == ShowCondition.HoldKey);
    }

    /// <param name="condition">The highlight's condition.</param>
    /// <param name="activity">Latest input activity.</param>
    /// <param name="now">Current timestamp in the ticks of <paramref name="frequency"/>.</param>
    /// <param name="frequency">Ticks per second (<see cref="TimeProvider.TimestampFrequency"/>).</param>
    public static bool IsVisible(ShowCondition condition, MarkerActivitySample activity, long now, long frequency) => condition switch
    {
        ShowCondition.Idle => !RecentlyActive(activity, now, frequency),
        ShowCondition.Moving => RecentlyActive(activity, now, frequency),
        ShowCondition.HoldKey => activity.HoldSince > 0 && now - activity.HoldSince >= Ticks(MinHoldTime, frequency)
            && now - activity.HoldSeen < Ticks(HoldTimeout, frequency),

        // Always, and anything unknown: as before E1.
        _ => true,
    };

    private static bool RecentlyActive(MarkerActivitySample activity, long now, long frequency) =>
        activity.LastActivity != 0 && now - activity.LastActivity < Ticks(RestTime, frequency);

    private static long Ticks(TimeSpan span, long frequency) => (long)(span.TotalSeconds * frequency);
}
