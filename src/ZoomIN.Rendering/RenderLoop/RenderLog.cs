using Microsoft.Extensions.Logging;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>Log messages of the render loop (event ids 3000–3099); never called per frame.</summary>
internal static partial class RenderLog
{
    [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Zoom pipeline ready for {Monitor}")]
    public static partial void PipelineReady(this ILogger logger, string monitor);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Information, Message = "Display changed on {Monitor}: {Reason}")]
    public static partial void DisplayChanged(this ILogger logger, string monitor, string reason);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Error, Message = "Zoom pipeline for {Monitor} failed")]
    public static partial void PipelineFailed(this ILogger logger, Exception exception, string monitor);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Information, Message = "Monitors: {Monitors}")]
    public static partial void Monitors(this ILogger logger, IEnumerable<string> monitors);

    [LoggerMessage(EventId = 3005, Level = LogLevel.Critical, Message = "A render thread does not stop; restarting ZoomIN via the watchdog")]
    public static partial void RenderThreadStuck(this ILogger logger);

    [LoggerMessage(EventId = 3006, Level = LogLevel.Error, Message = "Render thread without progress for over {Seconds} s; rebuilding the zoom pipeline")]
    public static partial void RenderThreadHung(this ILogger logger, double seconds);

    [LoggerMessage(EventId = 3007, Level = LogLevel.Warning, Message = "Smart inversion on {Monitor} falls back to keep hue: {Reason} ({Milliseconds} ms)")]
    public static partial void SmartInvertFallback(this ILogger logger, string monitor, string reason, double milliseconds);

    [LoggerMessage(EventId = 3008, Level = LogLevel.Information, Message = "Adaptive pointer on {Monitor} keeps the desktop copy running without zoom")]
    public static partial void AdaptivePointerCapture(this ILogger logger, string monitor);
}
