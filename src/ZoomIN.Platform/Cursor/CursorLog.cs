using Microsoft.Extensions.Logging;

namespace ZoomIN.Platform.Cursor;

/// <summary>Log messages of the pointer (event ids 2100–2199).</summary>
internal static partial class CursorLog
{
    [LoggerMessage(EventId = 2101, Level = LogLevel.Warning, Message = "MagInitialize failed: the system pointer stays visible while zoomed")]
    public static partial void MagnifierUnavailable(this ILogger logger);
}
