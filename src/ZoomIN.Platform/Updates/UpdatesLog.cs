using Microsoft.Extensions.Logging;
using ZoomIN.Core.Updates;

namespace ZoomIN.Platform.Updates;

/// <summary>Log messages of downloads and installation (event ids 2600–2699).</summary>
internal static partial class UpdatesLog
{
    [LoggerMessage(EventId = 2601, Level = LogLevel.Information, Message = "Install step for {Version}: {Result}")]
    public static partial void InstallStepResult(this ILogger logger, AppVersion version, UpdateResult result);

    [LoggerMessage(EventId = 2602, Level = LogLevel.Error, Message = "Starting the install step failed")]
    public static partial void InstallStepFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2603, Level = LogLevel.Information, Message = "Release list skipped: {Reason}")]
    public static partial void ReleaseListSkipped(this ILogger logger, string reason);

    [LoggerMessage(EventId = 2604, Level = LogLevel.Warning, Message = "Package {Package} rejected: checksum mismatch")]
    public static partial void ChecksumMismatch(this ILogger logger, string package);

    [LoggerMessage(EventId = 2605, Level = LogLevel.Information, Message = "Package {Package} not downloaded: {Reason}")]
    public static partial void NotDownloaded(this ILogger logger, string package, string reason);

    [LoggerMessage(EventId = 2606, Level = LogLevel.Information, Message = "Update {Previous} -> {Version} installed")]
    public static partial void Installed(this ILogger logger, AppVersion? previous, AppVersion version);

    [LoggerMessage(EventId = 2607, Level = LogLevel.Error, Message = "Installing {Version} failed")]
    public static partial void InstallFailed(this ILogger logger, Exception exception, AppVersion version);

    [LoggerMessage(EventId = 2608, Level = LogLevel.Information, Message = "Update: {Item} kept for now, still in use")]
    public static partial void KeptInUse(this ILogger logger, string item);
}
