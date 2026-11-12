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
            return new(WithoutNewHotkeyClashes(settings with { CenterMouse = null }), null);
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
        return new(WithoutNewHotkeyClashes(migrated), previous);
    }

    /// <summary>
    /// The M19 shortcuts (islands B1, freeze G2) came without a schema change, so a file without them
    /// gets their defaults: if the user already gave such a gesture to an older action, the new shortcut
    /// stays unassigned instead of failing as "unavailable" (as with the overview in schema 2). Applied
    /// at every load, and only to the factory gestures; another gesture the user recorded is never
    /// touched and wins over a factory one (a recorded gesture that equals the factory gesture cannot
    /// be told apart and gives way like it).
    /// </summary>
    public static AppSettings WithoutNewHotkeyClashes(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        // Recorded M19 gestures count as taken like older ones. Factory gestures are distinct (NewHotkeyClashTests),
        // so no factory gesture takes another's.
        HotkeyGesture[] taken =
        [
            .. OlderHotkeys(settings), settings.OverviewHotkey,
            .. Recorded(settings.IslandsHotkey, HotkeyGesture.ToggleIslands),
            .. Recorded(settings.IslandEditHotkey, HotkeyGesture.EditIslands),
            .. Recorded(settings.FreezeMonitorHotkey, HotkeyGesture.FreezeMonitor),
            .. Recorded(settings.NextMonitorHotkey, HotkeyGesture.NextMonitor),
        ];
        return settings with
        {
            IslandsHotkey = Unclashed(settings.IslandsHotkey, HotkeyGesture.ToggleIslands, taken),
            IslandEditHotkey = Unclashed(settings.IslandEditHotkey, HotkeyGesture.EditIslands, taken),
            FreezeMonitorHotkey = Unclashed(settings.FreezeMonitorHotkey, HotkeyGesture.FreezeMonitor, taken),
            NextMonitorHotkey = Unclashed(settings.NextMonitorHotkey, HotkeyGesture.NextMonitor, taken),
        };
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

    // A gesture the user recorded (not the factory one) is taken; empty otherwise.
    private static HotkeyGesture[] Recorded(HotkeyGesture gesture, HotkeyGesture factory) => gesture == factory ? [] : [gesture];

    // Only the factory default gives way (it may have come from a file without the property); a
    // gesture the user recorded on purpose stays, even if it is also used elsewhere.
    private static HotkeyGesture Unclashed(HotkeyGesture gesture, HotkeyGesture factory, IReadOnlyCollection<HotkeyGesture> taken) =>
        gesture == factory && taken.Contains(gesture) ? default : gesture;
}
