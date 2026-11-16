using ZoomIN.Core.Pointer;

namespace ZoomIN.Core.Tracking;

/// <summary>
/// Opacity of the three highlights of one monitor (E1): crosshair, caret marker and focus frame,
/// each following its own <see cref="ShowCondition"/>. Updated once per render pass; no allocation.
/// </summary>
public sealed class MarkerAlphas
{
    private MarkerFade _crosshair;
    private MarkerFade _caret;
    private MarkerFade _focus;

    public double Crosshair => _crosshair.Alpha;

    public double Caret => _caret.Alpha;

    public double Focus => _focus.Alpha;

    /// <summary>
    /// Per frame, from the options. A highlight that is off (crosshair not shown at this zoom,
    /// tracking off) heads for 0 and stays there: no redraws while off, and switched on it fades in
    /// instead of flashing at full strength before its condition takes over.
    /// </summary>
    /// <returns>True if any opacity changed: the frame must be drawn, even if nothing else changed.</returns>
    public bool Update(
        PointerStyle cursor, double zoom, TrackingOptions tracking, MarkerActivitySample activity, long now, long frequency)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        ArgumentNullException.ThrowIfNull(tracking);
        return Update(
            cursor.CrosshairVisible(zoom) && MarkerVisibility.IsVisible(cursor.CrosshairShow, activity, now, frequency),
            tracking.Enabled && tracking.CaretMarker && MarkerVisibility.IsVisible(tracking.CaretShow, activity, now, frequency),
            tracking.Enabled && tracking.FocusFrame && MarkerVisibility.IsVisible(tracking.FocusFrameShow, activity, now, frequency),
            now,
            frequency);
    }

    /// <returns>True if any opacity changed: the frame must be drawn, even if nothing else changed.</returns>
    public bool Update(
        ShowCondition crosshair, ShowCondition caret, ShowCondition focus, MarkerActivitySample activity, long now, long frequency) =>
        Update(
            MarkerVisibility.IsVisible(crosshair, activity, now, frequency),
            MarkerVisibility.IsVisible(caret, activity, now, frequency),
            MarkerVisibility.IsVisible(focus, activity, now, frequency),
            now,
            frequency);

    private bool Update(bool crosshair, bool caret, bool focus, long now, long frequency)
    {
        // Non-short-circuit: every fade advances, also when an earlier one already changed.
        var changed = _crosshair.Update(crosshair, now, frequency);
        changed |= _caret.Update(caret, now, frequency);
        changed |= _focus.Update(focus, now, frequency);
        return changed;
    }
}
