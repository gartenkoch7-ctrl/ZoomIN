using Microsoft.Extensions.Logging;

namespace ZoomIN.App.SecureDesktop;

/// <summary>Log messages of the secure-desktop instance and its switch (event ids 1100–1199).</summary>
internal static partial class SecureHostLog
{
    [LoggerMessage(EventId = 1101, Level = LogLevel.Warning, Message = "Secure desktop: off until Windows restarts (watchdog gave up after repeated crashes)")]
    public static partial void GaveUp(this ILogger logger);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Information, Message = "ZoomIN secure desktop started (SYSTEM: {IsSystem})")]
    public static partial void Started(this ILogger logger, bool isSystem);

    [LoggerMessage(EventId = 1103, Level = LogLevel.Information, Message = "ZoomIN secure desktop exits")]
    public static partial void Exits(this ILogger logger);

    [LoggerMessage(EventId = 1104, Level = LogLevel.Warning, Message = "Exit shortcut for the secure desktop is unavailable")]
    public static partial void ExitShortcutUnavailable(this ILogger logger);

    [LoggerMessage(EventId = 1105, Level = LogLevel.Information, Message = "Secure desktop engine running after {Milliseconds} ms (zoom on: {ZoomOn})")]
    public static partial void EngineRunning(this ILogger logger, double milliseconds, bool zoomOn);

    [LoggerMessage(EventId = 1106, Level = LogLevel.Warning, Message = "Secure desktop: no magnified image after {Seconds} s")]
    public static partial void NoFirstImage(this ILogger logger, double seconds);

    [LoggerMessage(EventId = 1107, Level = LogLevel.Information, Message = "Secure desktop: first magnified image after {Milliseconds} ms")]
    public static partial void FirstImage(this ILogger logger, double milliseconds);

    [LoggerMessage(EventId = 1108, Level = LogLevel.Information, Message = "Secure desktop support switched {State}")]
    public static partial void SupportSwitched(this ILogger logger, string state);

    [LoggerMessage(EventId = 1109, Level = LogLevel.Error, Message = "Changing the secure desktop registration failed")]
    public static partial void ChangeFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1110, Level = LogLevel.Error, Message = "Starting the secure desktop switch failed")]
    public static partial void SwitchStartFailed(this ILogger logger, Exception exception);
}
