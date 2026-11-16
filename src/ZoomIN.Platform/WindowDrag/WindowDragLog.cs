using Microsoft.Extensions.Logging;
using ZoomIN.Core.WindowDrag;

namespace ZoomIN.Platform.WindowDrag;

/// <summary>Log messages of the window drag source (G1, event ids 2900–2919). Process and class only, never titles.</summary>
internal static partial class WindowDragLog
{
    [LoggerMessage(EventId = 2900, Level = LogLevel.Debug, Message = "Window drag started: {Process} {WindowClass} as {Kind}")]
    public static partial void DragStarted(this ILogger logger, string process, string windowClass, WindowDragKind kind);

    [LoggerMessage(EventId = 2901, Level = LogLevel.Debug, Message = "Window drag ended ({Reason}) after {DurationMs} ms")]
    public static partial void DragEnded(this ILogger logger, string reason, long durationMs);

    [LoggerMessage(EventId = 2902, Level = LogLevel.Debug, Message = "Window drag kind unclear at the start (hit test {HitTest}); the first change decides")]
    public static partial void KindUnclear(this ILogger logger, int hitTest);

    [LoggerMessage(EventId = 2903, Level = LogLevel.Debug, Message = "Window drag kind from the first change: {Kind}")]
    public static partial void KindResolved(this ILogger logger, WindowDragKind kind);

    [LoggerMessage(EventId = 2904, Level = LogLevel.Information, Message = "Window drag source watches move and size events")]
    public static partial void SourceStarted(this ILogger logger);

    [LoggerMessage(EventId = 2905, Level = LogLevel.Warning, Message = "Window move and size events unavailable; the zoom stays while windows are dragged")]
    public static partial void HooksFailed(this ILogger logger);

    [LoggerMessage(EventId = 2906, Level = LogLevel.Error, Message = "Handling a window drag event failed")]
    public static partial void EventFailed(this ILogger logger, Exception exception);
}
