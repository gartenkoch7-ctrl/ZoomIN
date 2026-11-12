using Microsoft.Extensions.Logging;
using ZoomIN.Core.Hotkeys;

namespace ZoomIN.Platform.Input;

/// <summary>Log messages of hotkeys and input hooks (event ids 2200–2299, overview 2920–2921); never per key or mouse move.</summary>
internal static partial class InputLog
{
    [LoggerMessage(EventId = 2201, Level = LogLevel.Warning, Message = "Hotkey {Gesture} is already in use by another application")]
    public static partial void HotkeyInUse(this ILogger logger, HotkeyGesture gesture);

    [LoggerMessage(EventId = 2202, Level = LogLevel.Error, Message = "Hotkey handler failed")]
    public static partial void HotkeyHandlerFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2203, Level = LogLevel.Error, Message = "Installing the activity hooks failed; shake/Ctrl locator and hiding while typing are unavailable")]
    public static partial void ActivityHooksUnavailable(this ILogger logger);

    [LoggerMessage(EventId = 2204, Level = LogLevel.Error, Message = "Input activity handler failed")]
    public static partial void ActivityHandlerFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2205, Level = LogLevel.Error, Message = "Installing the mouse wheel hook failed; Ctrl+wheel zoom is unavailable")]
    public static partial void WheelHookUnavailable(this ILogger logger);

    [LoggerMessage(EventId = 2206, Level = LogLevel.Error, Message = "Wheel zoom handler failed")]
    public static partial void WheelHandlerFailed(this ILogger logger, Exception exception);

    // M17 lane B2 (overview) uses 2920–2939: 2920–2921 here, 2922 in ShellLog.
    [LoggerMessage(EventId = 2920, Level = LogLevel.Information, Message = "Overview: Esc swallowed {Count} times")]
    public static partial void OverviewEscapesSwallowed(this ILogger logger, int count);

    [LoggerMessage(EventId = 2921, Level = LogLevel.Error, Message = "Overview key handler failed")]
    public static partial void OverviewKeyHandlerFailed(this ILogger logger, Exception exception);
}
