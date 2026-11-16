using Microsoft.Extensions.Logging;
using ZoomIN.Core.Overview;

namespace ZoomIN.App.Overview;

/// <summary>Log messages of the overview (B2, event ids 1520–1539); one per overview, never per frame.</summary>
internal static partial class OverviewLog
{
    [LoggerMessage(EventId = 1520, Level = LogLevel.Information, Message = "Overview on {Monitor} (level before {Level:0.00})")]
    public static partial void OverviewStarted(this ILogger logger, string monitor, double level);

    [LoggerMessage(EventId = 1521, Level = LogLevel.Information, Message = "Overview ended: {End} (held: {Held})")]
    public static partial void OverviewEnded(this ILogger logger, OverviewEnd end, bool held);

    [LoggerMessage(EventId = 1522, Level = LogLevel.Error, Message = "Overview step {Step} failed")]
    public static partial void OverviewFailed(this ILogger logger, Exception exception, string step);
}
