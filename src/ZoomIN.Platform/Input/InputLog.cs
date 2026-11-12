using Microsoft.Extensions.Logging;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Tracking;

namespace ZoomIN.Platform.Input;

/// <summary>Log messages of hotkeys and input hooks (event ids 2200–2299, overview 2920–2921, hold key 2950–2959); never per key or mouse move.</summary>
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
    // Hold key of the highlights (E1, M18 lane C: 2950–2959).
    [LoggerMessage(EventId = 2950, Level = LogLevel.Information, Message = "Hold key hook started for {Key}")]
    public static partial void HoldKeyHookStarted(this ILogger logger, MarkerHoldKey key);

    [LoggerMessage(EventId = 2951, Level = LogLevel.Information, Message = "Hold key hook stopped")]
    public static partial void HoldKeyHookStopped(this ILogger logger);

    [LoggerMessage(EventId = 2952, Level = LogLevel.Error, Message = "Installing the hold key hook failed; highlights shown only while a key is held stay hidden")]
    public static partial void HoldKeyHookUnavailable(this ILogger logger);

    [LoggerMessage(EventId = 2953, Level = LogLevel.Error, Message = "Hold key handler failed")]
    public static partial void HoldKeyHandlerFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2954, Level = LogLevel.Warning, Message = "Hold key is down but the hook saw no press; following the key state instead")]
    public static partial void HoldKeyHookMissedPress(this ILogger logger);
}
