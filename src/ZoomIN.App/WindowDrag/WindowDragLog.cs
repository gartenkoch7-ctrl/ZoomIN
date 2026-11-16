using Microsoft.Extensions.Logging;
using ZoomIN.Core.WindowDrag;

namespace ZoomIN.App.WindowDrag;

/// <summary>Log messages of zoom while moving windows (G1, event ids 1540–1559). No window titles.</summary>
internal static partial class WindowDragLog
{
    [LoggerMessage(EventId = 1540, Level = LogLevel.Debug, Message = "Window drag ended on {Monitor}: {Kind}, {Action}, zoom {LevelBefore} held at {Level}, crossed monitors {Crossed}, cleared {Cleared}")]
    public static partial void DragDone(this ILogger logger, string monitor, WindowDragKind kind, WindowDragAction action, double levelBefore, double? level, bool crossed, bool cleared);

    [LoggerMessage(EventId = 1541, Level = LogLevel.Information, Message = "Monitors changed during a window drag; its zoom level was given back")]
    public static partial void MonitorsChanged(this ILogger logger);

    [LoggerMessage(EventId = 1542, Level = LogLevel.Error, Message = "Handling a window drag failed")]
    public static partial void DragFailed(this ILogger logger, Exception exception);
}
