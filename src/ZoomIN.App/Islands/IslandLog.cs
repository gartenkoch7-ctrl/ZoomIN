using Microsoft.Extensions.Logging;

namespace ZoomIN.App.Islands;

/// <summary>Log messages of the islands (M19 B1, event ids 1740–1759); per command or edit step, never per frame. Only rectangles and levels.</summary>
internal static partial class IslandLog
{
    [LoggerMessage(EventId = 1740, Level = LogLevel.Information, Message = "Islands shown: {Visible} ({Count} islands)")]
    public static partial void IslandsToggled(this ILogger logger, bool visible, int count);

    [LoggerMessage(EventId = 1741, Level = LogLevel.Information, Message = "Island edit mode on ({Count} islands)")]
    public static partial void EditStarted(this ILogger logger, int count);

    [LoggerMessage(EventId = 1742, Level = LogLevel.Information, Message = "Island edit mode off: {Reason} ({Count} islands, {Seconds:0.0} s)")]
    public static partial void EditEnded(this ILogger logger, string reason, int count, double seconds);

    [LoggerMessage(EventId = 1743, Level = LogLevel.Information, Message = "Island edit mode not started: {Reason}")]
    public static partial void EditRefused(this ILogger logger, string reason);

    [LoggerMessage(EventId = 1744, Level = LogLevel.Information,
        Message = "Island {Index} {Outcome} on {Monitor}: source {Source}, picture {Picture}, level {Level:0.00}, still {Still}")]
    public static partial void IslandChanged(
        this ILogger logger, int index, string outcome, string monitor, string source, string picture, double level, bool still);

    [LoggerMessage(EventId = 1745, Level = LogLevel.Information, Message = "New island refused: all {Max} places are taken")]
    public static partial void IslandRefused(this ILogger logger, int max);

    [LoggerMessage(EventId = 1746, Level = LogLevel.Information, Message = "Island removed ({Count} left)")]
    public static partial void IslandRemoved(this ILogger logger, int count);

    [LoggerMessage(EventId = 1748, Level = LogLevel.Information, Message = "Island {Index} changed on the settings page: {Change}")]
    public static partial void IslandChangedOnPage(this ILogger logger, int index, string change);

    [LoggerMessage(EventId = 1747, Level = LogLevel.Error, Message = "Island edit step {Step} failed")]
    public static partial void EditFailed(this ILogger logger, Exception exception, string step);
}
