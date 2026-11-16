using Microsoft.Extensions.Logging;

namespace ZoomIN.Platform.WindowFilters;

/// <summary>Log messages of the filter per window (event ids 2700–2799). Process names only, never titles.</summary>
internal static partial class WindowFilterLog
{
    [LoggerMessage(EventId = 2700, Level = LogLevel.Information, Message = "Filter per window watches windows ({Hooks} event hooks)")]
    public static partial void WindowFilterStarted(this ILogger logger, int hooks);

    [LoggerMessage(EventId = 2701, Level = LogLevel.Debug, Message = "Window of {Process} measured as {Result} in {DurationMs} ms after {Looks} looks, caption bar {Caption}")]
    public static partial void WindowClassified(this ILogger logger, string process, string result, double durationMs, int looks, string caption);

    [LoggerMessage(EventId = 2702, Level = LogLevel.Warning, Message = "Window events unavailable; the filter per window only follows shortcuts and settings")]
    public static partial void WindowHooksFailed(this ILogger logger);

    [LoggerMessage(EventId = 2703, Level = LogLevel.Error, Message = "Updating the window regions failed")]
    public static partial void WindowUpdateFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2704, Level = LogLevel.Warning, Message = "No bitmap for brightness looks; windows stay unmeasured")]
    public static partial void WindowSamplerFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2705, Level = LogLevel.Information, Message = "{Dropped} windows over the limit of {Max} regions show the monitor filter")]
    public static partial void WindowRegionsDropped(this ILogger logger, int dropped, int max);
}
