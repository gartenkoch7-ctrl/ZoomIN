using Microsoft.Extensions.Logging;

namespace ZoomIN.Platform.AgentDebug;

/// <summary>Log messages of the KI-Debug pipe (event ids 2800–2899, ADR-0027).</summary>
internal static partial class AgentDebugLog
{
    [LoggerMessage(EventId = 2801, Level = LogLevel.Information, Message = "KI-Debug pipe open")]
    public static partial void PipeOpened(this ILogger logger);

    [LoggerMessage(EventId = 2802, Level = LogLevel.Information, Message = "KI-Debug pipe closed")]
    public static partial void PipeClosed(this ILogger logger);

    [LoggerMessage(EventId = 2803, Level = LogLevel.Warning, Message = "KI-Debug connection failed")]
    public static partial void ConnectionFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2804, Level = LogLevel.Information, Message = "KI-Debug: ZoomIN not reachable ({Reason})")]
    public static partial void NotReachable(this ILogger logger, string reason);
}
