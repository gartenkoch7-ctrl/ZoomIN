using Microsoft.Extensions.Logging;

namespace ZoomIN.App.AgentDebug;

/// <summary>Log messages of KI-Debug in the app and the MCP role (event ids 1400–1499, ADR-0027).</summary>
internal static partial class AgentLog
{
    [LoggerMessage(EventId = 1401, Level = LogLevel.Information, Message = "KI-Debug allowed: {Allowed}")]
    public static partial void Allowed(this ILogger logger, bool allowed);

    [LoggerMessage(EventId = 1402, Level = LogLevel.Information, Message = "KI-Debug command {Tool}: {Result}")]
    public static partial void Command(this ILogger logger, string tool, string result);

    [LoggerMessage(EventId = 1403, Level = LogLevel.Error, Message = "KI-Debug command {Tool} failed")]
    public static partial void CommandFailed(this ILogger logger, Exception exception, string tool);

    [LoggerMessage(EventId = 1404, Level = LogLevel.Information, Message = "MCP server started")]
    public static partial void McpStarted(this ILogger logger);

    [LoggerMessage(EventId = 1405, Level = LogLevel.Information, Message = "MCP server ended")]
    public static partial void McpEnded(this ILogger logger);
}
