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
        };
        return new(migrated, previous);
    }

    /// <summary>What schema 1 did: centred unless switched off (missing = the default, on).</summary>
    public static MouseFollowMode LegacyMouseFollow(bool? centerMouse) =>
        centerMouse == false ? MouseFollowMode.Fixpoint : MouseFollowMode.Center;
}
