using Microsoft.Extensions.Logging;

namespace ZoomIN.App.Freeze;

/// <summary>Log messages of frozen monitors and the monitor switch (event ids 1720–1739, G2). Monitor ids and technical values only.</summary>
internal static partial class MonitorFreezeLog
{
    [LoggerMessage(EventId = 1720, Level = LogLevel.Information, Message = "Monitor {Monitor} frozen at {Level:0.00}x ({Image}); pointer moved to {Target}")]
    public static partial void MonitorFrozen(this ILogger logger, string monitor, double level, string image, string target);

    [LoggerMessage(EventId = 1721, Level = LogLevel.Information, Message = "Freeze lifted on {Count} monitors ({Reason})")]
    public static partial void FreezeLifted(this ILogger logger, int count, string reason);

    [LoggerMessage(EventId = 1722, Level = LogLevel.Information, Message = "Pointer switched from {From} to {To} (skipped {Skipped} frozen)")]
    public static partial void MonitorSwitched(this ILogger logger, string from, string to, int skipped);

    [LoggerMessage(EventId = 1723, Level = LogLevel.Information, Message = "Freezing refused: {Reason}")]
    public static partial void FreezeRefused(this ILogger logger, string reason);

    [LoggerMessage(EventId = 1724, Level = LogLevel.Information, Message = "Monitor switch refused: {Reason}")]
    public static partial void SwitchRefused(this ILogger logger, string reason);

    [LoggerMessage(EventId = 1725, Level = LogLevel.Error, Message = "Frozen monitors: {Step} failed; every freeze was lifted and the pointer released")]
    public static partial void FreezeFailed(this ILogger logger, Exception exception, string step);
}
