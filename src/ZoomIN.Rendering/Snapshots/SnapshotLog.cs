using Microsoft.Extensions.Logging;

namespace ZoomIN.Rendering.Snapshots;

/// <summary>Log messages of the debug snapshots (event ids 3050–3059); never per frame.</summary>
internal static partial class SnapshotLog
{
    [LoggerMessage(EventId = 3050, Level = LogLevel.Information, Message = "Debug snapshot of {Monitor} saved to {Folder}")]
    public static partial void SnapshotSaved(this ILogger logger, string monitor, string folder);

    [LoggerMessage(EventId = 3051, Level = LogLevel.Warning, Message = "Debug snapshot of {Monitor} failed")]
    public static partial void SnapshotFailed(this ILogger logger, Exception exception, string monitor);
}
