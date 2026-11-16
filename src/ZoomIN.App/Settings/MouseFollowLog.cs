using Microsoft.Extensions.Logging;
using ZoomIN.Core.Tracking;

namespace ZoomIN.App.Settings;

/// <summary>Log messages of the mouse follow mode (M17 A1, event ids 1500–1519).</summary>
internal static partial class MouseFollowLog
{
    [LoggerMessage(EventId = 1500, Level = LogLevel.Information, Message = "Settings migrated to schema {Version}: mouse follow {Previous} -> {Current}")]
    public static partial void MouseFollowMigrated(this ILogger logger, int version, MouseFollowMode previous, MouseFollowMode current);

    [LoggerMessage(EventId = 1501, Level = LogLevel.Information, Message = "Mouse follow mode changed: {Previous} -> {Current}")]
    public static partial void MouseFollowChanged(this ILogger logger, MouseFollowMode previous, MouseFollowMode current);

    [LoggerMessage(EventId = 1502, Level = LogLevel.Information, Message = "Mouse follow hint shown")]
    public static partial void MouseFollowHintShown(this ILogger logger);
}
