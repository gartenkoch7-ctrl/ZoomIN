using Microsoft.Extensions.Logging;
using ZoomIN.Core.Hotkeys;

namespace ZoomIN.App.SecureDesktop.Checks;

/// <summary>Start timings, checks and snapshot shortcut of the secure instance (event ids 1150–1169, M10 completion).</summary>
internal static partial class SecureChecksLog
{
    [LoggerMessage(EventId = 1150, Level = LogLevel.Information, Message = "Secure desktop start: pipeline after {PipelineMs} ms, first desktop copy after {CaptureMs} ms, first image after {ShownMs} ms, zoom level after {ZoomMs} ms")]
    public static partial void StartTimings(this ILogger logger, double pipelineMs, double captureMs, double shownMs, double zoomMs);

    [LoggerMessage(EventId = 1151, Level = LogLevel.Information, Message = "Secure desktop check: desktop {Desktop}")]
    public static partial void CheckDesktop(this ILogger logger, string desktop);

    [LoggerMessage(EventId = 1152, Level = LogLevel.Information, Message = "Secure desktop check: overlay on top at the centre of monitor {Monitor}")]
    public static partial void CheckOnTop(this ILogger logger, string monitor);

    [LoggerMessage(EventId = 1153, Level = LogLevel.Warning, Message = "Secure desktop check: overlay not on top at the centre of monitor {Monitor} (top window class {WindowClass})")]
    public static partial void CheckNotOnTop(this ILogger logger, string monitor, string windowClass);

    [LoggerMessage(EventId = 1154, Level = LogLevel.Information, Message = "Secure desktop check: overlay 0x{Window:X} excluded from capture")]
    public static partial void CheckAffinity(this ILogger logger, long window);

    [LoggerMessage(EventId = 1155, Level = LogLevel.Warning, Message = "Secure desktop check: overlay 0x{Window:X} not excluded from capture (display affinity {Affinity})")]
    public static partial void CheckAffinityFailed(this ILogger logger, long window, string affinity);

    [LoggerMessage(EventId = 1156, Level = LogLevel.Information, Message = "Secure desktop check: no overlay on screen")]
    public static partial void CheckNoOverlay(this ILogger logger);

    [LoggerMessage(EventId = 1157, Level = LogLevel.Information, Message = "Secure desktop check: process in a job: {InJob}")]
    public static partial void CheckJob(this ILogger logger, string inJob);

    [LoggerMessage(EventId = 1158, Level = LogLevel.Information, Message = "Secure desktop check: watchdog {ProcessId} running, in a job: {InJob}")]
    public static partial void CheckWatchdog(this ILogger logger, int processId, string inJob);

    [LoggerMessage(EventId = 1159, Level = LogLevel.Warning, Message = "Secure desktop check: no watchdog known (not started, or the shell gave no id)")]
    public static partial void CheckNoWatchdog(this ILogger logger);

    [LoggerMessage(EventId = 1160, Level = LogLevel.Warning, Message = "Secure desktop check: shortcut {Gesture} unavailable")]
    public static partial void CheckShortcutUnavailable(this ILogger logger, HotkeyGesture gesture);

    [LoggerMessage(EventId = 1161, Level = LogLevel.Information, Message = "Secure desktop check: {Unavailable} shortcut(s) unavailable, exit shortcut registered: {Exit}, snapshot shortcut registered: {Snapshot}")]
    public static partial void CheckShortcuts(this ILogger logger, int unavailable, bool exit, bool snapshot);

    [LoggerMessage(EventId = 1162, Level = LogLevel.Error, Message = "Secure desktop checks failed")]
    public static partial void ChecksFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1163, Level = LogLevel.Information, Message = "Secure desktop snapshot requested")]
    public static partial void SnapshotRequested(this ILogger logger);

    [LoggerMessage(EventId = 1164, Level = LogLevel.Information, Message = "Secure desktop snapshot: {Saved} monitor(s) saved in {Folder}")]
    public static partial void SnapshotSaved(this ILogger logger, int saved, string folder);

    [LoggerMessage(EventId = 1165, Level = LogLevel.Warning, Message = "Secure desktop snapshot: nothing saved (no monitor drew in time)")]
    public static partial void SnapshotNothing(this ILogger logger);

    [LoggerMessage(EventId = 1166, Level = LogLevel.Warning, Message = "Snapshot shortcut for the secure desktop is unavailable")]
    public static partial void SnapshotShortcutUnavailable(this ILogger logger);

    [LoggerMessage(EventId = 1167, Level = LogLevel.Warning, Message = "Secure desktop: zoom level not reached after {Seconds} s")]
    public static partial void ZoomLevelNotReached(this ILogger logger, double seconds);

    [LoggerMessage(EventId = 1168, Level = LogLevel.Error, Message = "Secure desktop snapshot failed")]
    public static partial void SnapshotFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1169, Level = LogLevel.Warning, Message = "Secure desktop check: watchdog {ProcessId} is not running (in a job: {InJob})")]
    public static partial void CheckWatchdogGone(this ILogger logger, int processId, string inJob);
}
