using Microsoft.Extensions.Logging;

namespace ZoomIN.Rendering.TextEnhance;

/// <summary>Log messages of "Schärfe"/"Fett" on the render side (event ids 3200–3219, M18, D1); never per frame.</summary>
internal static partial class EnhanceLog
{
    [LoggerMessage(EventId = 3200, Level = LogLevel.Information, Message = "Text enhancement {State}: sharpness {Sharpness} %, bold {Bold} px, smoothing {Smoothing}")]
    public static partial void StateChanged(this ILogger logger, string state, int sharpness, double bold, string smoothing);

    [LoggerMessage(EventId = 3201, Level = LogLevel.Debug, Message = "Text enhancement values changed: sharpness {Sharpness} %, bold {Bold} px")]
    public static partial void ValuesChanged(this ILogger logger, int sharpness, double bold);
}
