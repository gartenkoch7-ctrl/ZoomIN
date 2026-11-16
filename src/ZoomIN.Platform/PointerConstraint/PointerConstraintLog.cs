using Microsoft.Extensions.Logging;
using ZoomIN.Core.Pointer;

namespace ZoomIN.Platform.PointerConstraint;

/// <summary>
/// Log messages of the axis lock key watch (A6, event ids 2960–2979). Never from the keyboard hook:
/// it only sets flags, the check timer reports.
/// </summary>
internal static partial class PointerConstraintLog
{
    [LoggerMessage(EventId = 2960, Level = LogLevel.Information, Message = "Axis lock key watch started for {Key}")]
    public static partial void KeyWatchStarted(this ILogger logger, AxisLockKey key);

    [LoggerMessage(EventId = 2961, Level = LogLevel.Information, Message = "Axis lock key watch stopped")]
    public static partial void KeyWatchStopped(this ILogger logger);

    [LoggerMessage(EventId = 2962, Level = LogLevel.Warning, Message = "Axis lock: installing the keyboard hook failed; the axis lock does not react to its key")]
    public static partial void KeyHookUnavailable(this ILogger logger);

    [LoggerMessage(EventId = 2963, Level = LogLevel.Debug, Message = "Axis lock key release was missed (e.g. another desktop was active); released by the check")]
    public static partial void ReleaseMissed(this ILogger logger);

    [LoggerMessage(EventId = 2964, Level = LogLevel.Error, Message = "Axis lock key handler failed {Count} times")]
    public static partial void HandlerFailed(this ILogger logger, int count);
}
