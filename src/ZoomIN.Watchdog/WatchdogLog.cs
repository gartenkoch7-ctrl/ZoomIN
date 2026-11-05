using Microsoft.Extensions.Logging;

namespace ZoomIN.Watchdog;

/// <summary>Log messages of the watchdog (event ids 4000–4099, see docs/kb/telemetrie.md).</summary>
internal static partial class WatchdogLog
{
    [LoggerMessage(EventId = 4001, Level = LogLevel.Warning, Message = "Cannot watch process {ProcessId}: {Reason}")]
    public static partial void CannotWatch(this ILogger logger, int processId, string reason);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Error, Message = "ZoomIN ended unexpectedly (exit code 0x{ExitCode:X8})")]
    public static partial void UnexpectedExit(this ILogger logger, int exitCode);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Warning, Message = "Restarting ZoomIN")]
    public static partial void Restarting(this ILogger logger);

    [LoggerMessage(EventId = 4004, Level = LogLevel.Error, Message = "Giving up after {Restarts} restarts within {Minutes} minutes")]
    public static partial void GivingUp(this ILogger logger, int restarts, double minutes);

    [LoggerMessage(EventId = 4005, Level = LogLevel.Information, Message = "Cleanup {Step} done")]
    public static partial void CleanupDone(this ILogger logger, string step);

    [LoggerMessage(EventId = 4006, Level = LogLevel.Error, Message = "Cleanup {Step} failed")]
    public static partial void CleanupFailed(this ILogger logger, Exception exception, string step);
}
