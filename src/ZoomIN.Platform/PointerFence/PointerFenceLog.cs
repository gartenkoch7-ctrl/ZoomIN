using Microsoft.Extensions.Logging;
using ZoomIN.Core.PointerFence;

namespace ZoomIN.Platform.PointerFence;

/// <summary>
/// Log messages of the pointer fence and the pointer mover (event ids 2940–2949, M19 step 0).
/// Never from the mouse hook: it only counts, the watcher thread reports.
/// </summary>
internal static partial class PointerFenceLog
{
    [LoggerMessage(EventId = 2940, Level = LogLevel.Information, Message = "Pointer fence {Mode} after {Trigger} ({Forbidden} forbidden areas, {Filters} filters)")]
    public static partial void FenceChanged(this ILogger logger, FenceMode mode, FenceTrigger trigger, int forbidden, int filters);

    [LoggerMessage(EventId = 2941, Level = LogLevel.Warning, Message = "Pointer fence: watching desktop switches failed; the fence is not re-applied after Win+L or UAC")]
    public static partial void WatcherUnavailable(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2942, Level = LogLevel.Warning, Message = "Pointer fence: nothing would be left for the pointer; the pointer stays free")]
    public static partial void NothingAllowed(this ILogger logger);

    [LoggerMessage(EventId = 2943, Level = LogLevel.Error, Message = "Pointer fence: installing the mouse hook failed; the pointer stays free")]
    public static partial void HookUnavailable(this ILogger logger);

    [LoggerMessage(EventId = 2944, Level = LogLevel.Warning, Message = "Pointer fence: ClipCursor failed (error {Error}); retried on the next check")]
    public static partial void ClipFailed(this ILogger logger, int error);

    [LoggerMessage(EventId = 2945, Level = LogLevel.Information, Message = "Pointer moved to {Monitor} by command ({Reason})")]
    public static partial void PointerMoved(this ILogger logger, string monitor, PointerMoveReason reason);

    [LoggerMessage(EventId = 2946, Level = LogLevel.Warning, Message = "Moving the pointer ({Reason}) failed (error {Error})")]
    public static partial void MoveFailed(this ILogger logger, PointerMoveReason reason, int error);

    [LoggerMessage(EventId = 2947, Level = LogLevel.Error, Message = "Pointer fence: {Count} move filter failures; those moves were let through")]
    public static partial void FilterFailures(this ILogger logger, long count);

    [LoggerMessage(EventId = 2948, Level = LogLevel.Information, Message = "Pointer fence: another program confines the pointer; not overriding it")]
    public static partial void ForeignClip(this ILogger logger);

    [LoggerMessage(EventId = 2949, Level = LogLevel.Error, Message = "Pointer fence update failed ({Trigger}); the pointer is released")]
    public static partial void UpdateFailed(this ILogger logger, Exception exception, FenceTrigger trigger);
}
