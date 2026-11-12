using Microsoft.Extensions.Logging;
using ZoomIN.Core.Settings;

namespace ZoomIN.App.Settings;

/// <summary>
/// Loads the settings at start and brings an older file up to date (<see cref="SettingsMigration"/>).
/// A migrated file is saved right away, so the migration runs exactly once even if ZoomIN ends
/// before anything else changes.
/// </summary>
public sealed class SettingsLoader
{
    private readonly ISettingsStore _store;
    private readonly ILogger _log;
    private SettingsMigrationResult? _migration;

    public SettingsLoader(ISettingsStore store, ILogger<SettingsLoader> log)
    {
        _store = store;
        _log = log;
    }

    public AppSettings Load()
    {
        var result = SettingsMigration.Migrate(_store.Load());
        if (result.PreviousMouseFollow is not null)
        {
            _store.Save(result.Settings);
            _migration = result;
        }

        return result.Settings;
    }

    /// <summary>
    /// Logs a migration of this start (log 1500). Called once the telemetry level from these
    /// settings is applied: logged during Load it would be filtered by the start-up level.
    /// </summary>
    public void ReportMigration()
    {
        if (_migration is { PreviousMouseFollow: { } previous } migration)
        {
            _log.MouseFollowMigrated(migration.Settings.SchemaVersion, previous, migration.Settings.MouseFollow);
            _migration = null;
        }
    }
}
