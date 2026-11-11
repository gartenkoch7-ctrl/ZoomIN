using Microsoft.Extensions.Logging;

namespace ZoomIN.Platform.Automation;

/// <summary>Log messages of focus and caret tracking (event ids 2000–2099).</summary>
internal static partial class AutomationLog
{
    [LoggerMessage(EventId = 2001, Level = LogLevel.Error, Message = "UI Automation is unavailable; focus tracking is off")]
    public static partial void AutomationUnavailable(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Caret source in {Process}: {Source}")]
    public static partial void CaretSourceSeen(this ILogger logger, string process, string source);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Error, Message = "Reading focus and caret failed; tracking goes on with the system caret")]
    public static partial void ReadFailed(this ILogger logger, Exception exception);
}
