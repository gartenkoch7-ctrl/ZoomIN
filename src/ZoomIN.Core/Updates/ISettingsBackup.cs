namespace ZoomIN.Core.Updates;

/// <summary>Copies settings and profiles aside before a version change (ADR-0014, downgrades).</summary>
public interface ISettingsBackup
{
    /// <returns>The backup folder, or null if there was nothing to back up or copying failed.</returns>
    string? Backup(AppVersion from);
}
