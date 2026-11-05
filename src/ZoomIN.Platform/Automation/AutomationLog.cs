using Microsoft.Extensions.Logging;

namespace ZoomIN.Platform.Automation;

/// <summary>Log messages of focus and caret tracking (event ids 2000–2099).</summary>
internal static partial class AutomationLog
{
    [LoggerMessage(EventId = 2001, Level = LogLevel.Error, Message = "UI Automation is unavailable; focus tracking is off")]
    public static partial void AutomationUnavailable(this ILogger logger, Exception exception);
}
