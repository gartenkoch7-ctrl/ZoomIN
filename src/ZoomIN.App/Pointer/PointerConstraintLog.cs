using Microsoft.Extensions.Logging;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.PointerFence;

namespace ZoomIN.App.Pointer;

/// <summary>Log messages of mouse buffer and axis lock (A6, event ids 1760–1779). No coordinates per move.</summary>
internal static partial class PointerConstraintLog
{
    [LoggerMessage(EventId = 1760, Level = LogLevel.Information, Message = "Pointer constraint: buffer {Buffer}, axis lock {AxisLock} with {Key}, {Monitors} monitors")]
    public static partial void Applied(this ILogger logger, EdgeBufferLevel buffer, bool axisLock, AxisLockKey key, int monitors);

    [LoggerMessage(EventId = 1761, Level = LogLevel.Debug, Message = "Pointer constraint re-applied after {Trigger} ({Monitors} monitors)")]
    public static partial void Reapplied(this ILogger logger, FenceTrigger trigger, int monitors);

    [LoggerMessage(EventId = 1762, Level = LogLevel.Error, Message = "Applying the pointer constraint failed; buffer and axis lock are off")]
    public static partial void ApplyFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1763, Level = LogLevel.Error, Message = "Re-applying the pointer constraint after {Trigger} failed")]
    public static partial void ReapplyFailed(this ILogger logger, Exception exception, FenceTrigger trigger);
}
