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
            return new(settings with { CenterMouse = null }, null);
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
        return new(migrated, previous);
    }

    /// <summary>What schema 1 did: centred unless switched off (missing = the default, on).</summary>
    public static MouseFollowMode LegacyMouseFollow(bool? centerMouse) =>
        centerMouse == false ? MouseFollowMode.Fixpoint : MouseFollowMode.Center;

    // The overview shortcut (B2) is new in schema 2: if the user already gave its default to
    // another action, it stays unassigned instead of failing as "unavailable" or taking it over.
    private static HotkeyGesture OverviewHotkeyFor(AppSettings old)
    {
        HotkeyGesture[] taken =
        [
            old.ToggleHotkey, old.ZoomInHotkey, old.ZoomOutHotkey, old.FullscreenHotkey, old.LensHotkey, old.DockedHotkey,
            old.FilterHotkey, old.NextFilterHotkey, old.LocateHotkey, old.CrosshairHotkey, old.TrackingHotkey, old.ProfileHotkey,
            old.WindowFilterHotkey,
        ];
        return taken.Contains(HotkeyGesture.Overview) ? default : old.OverviewHotkey;
    }
}
