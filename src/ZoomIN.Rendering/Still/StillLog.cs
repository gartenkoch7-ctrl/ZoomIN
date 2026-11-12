using Microsoft.Extensions.Logging;

namespace ZoomIN.Rendering.Still;

/// <summary>Log messages of still images (event ids 3300–3309, M19 step 0); only when a texture is made or freed, never per frame.</summary>
internal static partial class StillLog
{
    [LoggerMessage(EventId = 3300, Level = LogLevel.Information, Message = "Still image of {Width}x{Height} px held")]
    public static partial void Held(this ILogger logger, int width, int height);

    [LoggerMessage(EventId = 3301, Level = LogLevel.Information, Message = "Still image released")]
    public static partial void Released(this ILogger logger);

    [LoggerMessage(EventId = 3302, Level = LogLevel.Warning, Message = "Still image not taken: source format {Format} is not supported")]
    public static partial void UnsupportedFormat(this ILogger logger, string format);
}
