using Microsoft.Extensions.Logging;

namespace ZoomIN.Rendering.Freeze;

/// <summary>Log messages of frozen monitors on the render thread (event ids 3310–3319, G2); only when a freeze starts or ends, never per frame.</summary>
internal static partial class FreezeLog
{
    [LoggerMessage(EventId = 3310, Level = LogLevel.Information, Message = "Frozen view on {Monitor} ({Image})")]
    public static partial void FrozenViewStarted(this ILogger logger, string monitor, string image);

    [LoggerMessage(EventId = 3311, Level = LogLevel.Information, Message = "Frozen view on {Monitor} ended")]
    public static partial void FrozenViewEnded(this ILogger logger, string monitor);

    [LoggerMessage(EventId = 3312, Level = LogLevel.Warning, Message = "Still image of frozen monitor {Monitor} not taken or lost with a rebuilt pipeline; it stays live")]
    public static partial void StillNotTaken(this ILogger logger, string monitor);
}
