using Microsoft.Extensions.Logging;

namespace ZoomIN.Rendering.Schemes;

/// <summary>Log messages of the own colour schemes on the render side (event ids 3220–3239, M18); never per frame.</summary>
internal static partial class SchemeLog
{
    [LoggerMessage(EventId = 3220, Level = LogLevel.Information, Message = "Filter palette rebuilt: {Count} own schemes ({Places})")]
    public static partial void PaletteRebuilt(this ILogger logger, int count, string places);

    [LoggerMessage(EventId = 3221, Level = LogLevel.Information, Message = "Colour replacement step {State}: {Replacements} replacements")]
    public static partial void ReplaceStep(this ILogger logger, string state, int replacements);
}
