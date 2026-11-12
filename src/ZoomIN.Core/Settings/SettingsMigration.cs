using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Tracking;

namespace ZoomIN.Core.Settings;

/// <summary>
/// Brings settings.json of an older version up to <see cref="AppSettings.CurrentSchemaVersion"/>.
/// Pure: the caller loads, logs and saves.
/// </summary>
public static class SettingsMigration
{
    /// <summary>
    /// Schema 1 → 2 (M17 A1): the mouse follows with the margin once, whatever "keep the pointer
    /// centred" was, and the user is told once where to change it back. Files of schema 2 or
    /// newer keep the user's choice.
    /// </summary>
    public static SettingsMigrationResult Migrate(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.SchemaVersion >= 2)
        {
            return new(WithoutFreezeClashes(settings with { CenterMouse = null }), null);
        }

        var previous = LegacyMouseFollow(settings.CenterMouse);
        var migrated = settings with
        {
            SchemaVersion = AppSettings.CurrentSchemaVersion,
            MouseFollow = MouseFollowMode.Margin,
            MouseFollowHintPending = true,
            CenterMouse = null,
            OverviewHotkey = OverviewHotkeyFor(settings),
        };
        return new(WithoutFreezeClashes(migrated), previous);
    }

    /// <summary>
    /// The freeze shortcuts (G2, M19) came without a schema change, so a file without them gets their
    /// defaults: if the user already gave such a gesture to an older action, the new shortcut stays
    /// unassigned instead of failing as "unavailable" (as with the overview in schema 2). Applied at
    /// every load, and only to the factory gestures: one the user recorded is never touched.
    /// </summary>
    public static AppSettings WithoutFreezeClashes(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        HotkeyGesture[] older = [.. OlderHotkeys(settings), settings.OverviewHotkey];
        var freeze = Unclashed(settings.FreezeMonitorHotkey, HotkeyGesture.FreezeMonitor, older);
        var next = Unclashed(settings.NextMonitorHotkey, HotkeyGesture.NextMonitor, [.. older, freeze]);
        return freeze == settings.FreezeMonitorHotkey && next == settings.NextMonitorHotkey
            ? settings
            : settings with { FreezeMonitorHotkey = freeze, NextMonitorHotkey = next };
    }

    /// <summary>What schema 1 did: centred unless switched off (missing = the default, on).</summary>
    public static MouseFollowMode LegacyMouseFollow(bool? centerMouse) =>
        centerMouse == false ? MouseFollowMode.Fixpoint : MouseFollowMode.Center;

    // The overview shortcut (B2) is new in schema 2: if the user already gave its default to
    // another action, it stays unassigned instead of failing as "unavailable" or taking it over.
    private static HotkeyGesture OverviewHotkeyFor(AppSettings old) =>
        OlderHotkeys(old).Contains(HotkeyGesture.Overview) ? default : old.OverviewHotkey;

    // The shortcuts every version had (M7–M12); a newer one gives way to them.
    private static HotkeyGesture[] OlderHotkeys(AppSettings s) =>
    [
        s.ToggleHotkey, s.ZoomInHotkey, s.ZoomOutHotkey, s.FullscreenHotkey, s.LensHotkey, s.DockedHotkey, s.FilterHotkey,
        s.NextFilterHotkey, s.LocateHotkey, s.CrosshairHotkey, s.TrackingHotkey, s.ProfileHotkey, s.WindowFilterHotkey,
    ];

    // Only the factory default gives way (it may have come from a file without the property); a
    // gesture the user recorded on purpose stays, even if it is also used elsewhere.
    private static HotkeyGesture Unclashed(HotkeyGesture gesture, HotkeyGesture factory, HotkeyGesture[] taken) =>
        gesture == factory && taken.Contains(gesture) ? default : gesture;
}
