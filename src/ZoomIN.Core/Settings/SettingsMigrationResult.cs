using ZoomIN.Core.Tracking;

namespace ZoomIN.Core.Settings;

/// <summary>Outcome of <see cref="SettingsMigration.Migrate"/>.</summary>
/// <param name="Settings">The settings to use (and save).</param>
/// <param name="PreviousMouseFollow">Set when the mouse follow mode was migrated: what the old version did.</param>
public sealed record SettingsMigrationResult(AppSettings Settings, MouseFollowMode? PreviousMouseFollow);
