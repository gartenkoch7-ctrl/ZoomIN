using Microsoft.Extensions.Logging;
using ZoomIN.Core.Hotkeys;

namespace ZoomIN.App.Zoom;

/// <summary>Log messages of the zoom controller (event ids 1300–1399).</summary>
internal static partial class ZoomLog
{
    [LoggerMessage(EventId = 1301, Level = LogLevel.Debug, Message = "Hotkey {Gesture} registered")]
    public static partial void HotkeyRegistered(this ILogger logger, HotkeyGesture gesture);
}
