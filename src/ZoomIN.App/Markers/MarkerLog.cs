using Microsoft.Extensions.Logging;
using ZoomIN.Core.Tracking;

namespace ZoomIN.App.Markers;

/// <summary>Log messages of the highlights (M18 lane C: E2 caret shapes 1640–1649, E1 conditions 1650–1659).</summary>
internal static partial class MarkerLog
{
    [LoggerMessage(EventId = 1640, Level = LogLevel.Information, Message = "Caret marker changed: {Shape}, {Thickness}")]
    public static partial void CaretStyleChanged(this ILogger logger, CaretMarkerShape shape, CaretMarkerThickness thickness);

    [LoggerMessage(EventId = 1650, Level = LogLevel.Information, Message = "Show condition of {Marker} changed: {Condition}")]
    public static partial void ShowConditionChanged(this ILogger logger, string marker, ShowCondition condition);

    [LoggerMessage(EventId = 1651, Level = LogLevel.Information, Message = "Hold key changed: {Key}")]
    public static partial void HoldKeyChanged(this ILogger logger, MarkerHoldKey key);
}
