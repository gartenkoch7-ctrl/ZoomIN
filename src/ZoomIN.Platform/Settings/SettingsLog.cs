using Microsoft.Extensions.Logging;

namespace ZoomIN.Platform.Settings;

/// <summary>
/// Log messages of settings and profiles (event ids 2400–2499). Only file and folder names: full
/// paths contain the Windows user name (ADR-0019).
/// </summary>
internal static partial class SettingsLog
{
    [LoggerMessage(EventId = 2401, Level = LogLevel.Information, Message = "Settings backed up to {Folder}")]
    public static partial void BackedUp(this ILogger logger, string folder);

    [LoggerMessage(EventId = 2402, Level = LogLevel.Error, Message = "Settings backup failed")]
    public static partial void BackupFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2403, Level = LogLevel.Warning, Message = "Profile {File} is unreadable: {Reason}")]
    public static partial void ProfileUnreadable(this ILogger logger, string file, string reason);

    [LoggerMessage(EventId = 2404, Level = LogLevel.Error, Message = "Settings file {File} is unreadable; using defaults")]
    public static partial void SettingsUnreadable(this ILogger logger, Exception exception, string file);

    [LoggerMessage(EventId = 2405, Level = LogLevel.Error, Message = "Saving settings to {File} failed")]
    public static partial void SaveFailed(this ILogger logger, Exception exception, string file);

    [LoggerMessage(EventId = 2406, Level = LogLevel.Warning, Message = "Could not back up broken settings file: {Reason}")]
    public static partial void BrokenBackupFailed(this ILogger logger, string reason);
}
