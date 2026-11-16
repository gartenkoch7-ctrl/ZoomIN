using Microsoft.Extensions.Logging;

namespace ZoomIN.Platform.Tests.Diagnostics;

/// <summary>Log messages used by the telemetry tests (event ids 9000–9099 are reserved for tests).</summary>
internal static partial class TestLog
{
    [LoggerMessage(EventId = 9001, Level = LogLevel.Information, Message = "{Text}")]
    public static partial void Info(this ILogger logger, string text);

    [LoggerMessage(EventId = 9002, Level = LogLevel.Warning, Message = "{Text}")]
    public static partial void Warning(this ILogger logger, string text);

    [LoggerMessage(EventId = 9003, Level = LogLevel.Error, Message = "{Text}")]
    public static partial void Failure(this ILogger logger, Exception? exception, string text);
}
