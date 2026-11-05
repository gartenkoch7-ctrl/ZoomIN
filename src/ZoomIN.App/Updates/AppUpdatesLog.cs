using Microsoft.Extensions.Logging;
using ZoomIN.Core.Updates;

namespace ZoomIN.App.Updates;

/// <summary>Log messages of update check, setup start, update runner and uninstall (event ids 1200–1299).</summary>
internal static partial class AppUpdatesLog
{
    [LoggerMessage(EventId = 1201, Level = LogLevel.Information, Message = "Setup: release list unavailable: {Reason}")]
    public static partial void SetupReleaseListUnavailable(this ILogger logger, string reason);

    [LoggerMessage(EventId = 1202, Level = LogLevel.Warning, Message = "Setup {Version} not started: {Reason}")]
    public static partial void SetupNotStarted(this ILogger logger, AppVersion version, string reason);

    [LoggerMessage(EventId = 1203, Level = LogLevel.Information, Message = "Setup {Version} started")]
    public static partial void SetupStarted(this ILogger logger, AppVersion version);

    [LoggerMessage(EventId = 1204, Level = LogLevel.Information, Message = "ZoomIN uninstalled")]
    public static partial void Uninstalled(this ILogger logger);

    [LoggerMessage(EventId = 1205, Level = LogLevel.Information, Message = "Uninstall: removal scheduled")]
    public static partial void RemovalScheduled(this ILogger logger);

    [LoggerMessage(EventId = 1206, Level = LogLevel.Error, Message = "Uninstall: starting the removal failed")]
    public static partial void RemovalFailed(this ILogger logger, Exception exception);

    // No path: a portable ZoomIN.exe lies in the user's folders (ADR-0019).
    [LoggerMessage(EventId = 1207, Level = LogLevel.Error, Message = "Update: this ZoomIN.exe is not a versioned installation")]
    public static partial void NotVersioned(this ILogger logger);

    [LoggerMessage(EventId = 1208, Level = LogLevel.Error, Message = "Update: refreshing the Apps entry failed")]
    public static partial void AppsEntryFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1209, Level = LogLevel.Information, Message = "Update available: {Version}")]
    public static partial void UpdateAvailable(this ILogger logger, AppVersion version);
}
