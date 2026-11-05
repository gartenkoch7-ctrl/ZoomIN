using Microsoft.Extensions.Logging;

namespace ZoomIN.App.Lifecycle;

/// <summary>Log messages of start, exit, crashes and the watchdog start (event ids 1000–1099).</summary>
internal static partial class LifecycleLog
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "ZoomIN started after {Milliseconds} ms")]
    public static partial void Started(this ILogger logger, double milliseconds);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "ZoomIN exits")]
    public static partial void Exits(this ILogger logger);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Error, Message = "Unhandled UI exception")]
    public static partial void UnhandledUiException(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Critical, Message = "Unhandled exception, ZoomIN ends")]
    public static partial void UnhandledException(this ILogger logger, Exception? exception);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Error, Message = "Unobserved task exception")]
    public static partial void UnobservedTaskException(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Warning, Message = "UIAccess is not active: quick settings and Start stay above the zoom")]
    public static partial void UiAccessInactive(this ILogger logger);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Information, Message = "Update checks stopped: {Reason}")]
    public static partial void UpdateChecksStopped(this ILogger logger, string reason);

    [LoggerMessage(EventId = 1008, Level = LogLevel.Information, Message = "Debugger attached: watchdog not started")]
    public static partial void WatchdogSkipped(this ILogger logger);

    [LoggerMessage(EventId = 1009, Level = LogLevel.Information, Message = "Watchdog started")]
    public static partial void WatchdogStarted(this ILogger logger);

    [LoggerMessage(EventId = 1010, Level = LogLevel.Error, Message = "Starting the watchdog failed")]
    public static partial void WatchdogFailed(this ILogger logger, Exception exception);
}
