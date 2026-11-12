using Microsoft.Extensions.Logging;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Core.Modes;

namespace ZoomIN.Platform.Shell;

/// <summary>Log messages of tray and docked strip (event ids 2500–2599; screen reader announcements 2922, M17 B2).</summary>
internal static partial class ShellLog
{
    [LoggerMessage(EventId = 2501, Level = LogLevel.Information, Message = "Docked strip reserved: {Edge} {Left},{Top}–{Right},{Bottom}")]
    public static partial void StripReserved(this ILogger logger, DockEdge edge, int left, int top, int right, int bottom);

    [LoggerMessage(EventId = 2502, Level = LogLevel.Information, Message = "Docked strip released")]
    public static partial void StripReleased(this ILogger logger);

    [LoggerMessage(EventId = 2503, Level = LogLevel.Warning, Message = "Adding the tray icon failed")]
    public static partial void TrayIconFailed(this ILogger logger);

    [LoggerMessage(EventId = 2504, Level = LogLevel.Error, Message = "Tray command {Command} failed")]
    public static partial void TrayCommandFailed(this ILogger logger, Exception exception, TrayCommand command);

    [LoggerMessage(EventId = 2922, Level = LogLevel.Warning, Message = "Screen reader announcement unavailable: {Reason}")]
    public static partial void AnnouncerUnavailable(this ILogger logger, string reason);
}
