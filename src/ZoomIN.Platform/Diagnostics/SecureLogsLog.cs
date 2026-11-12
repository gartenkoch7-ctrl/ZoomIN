using Microsoft.Extensions.Logging;

namespace ZoomIN.Platform.Diagnostics;

/// <summary>
/// Logs of fetching the secure instance's logs for the diagnostics package (event ids 2340–2359, M10).
/// Written by the tray app only: the elevated step itself keeps no log (see <see cref="SecureLogCopy"/>).
/// </summary>
internal static partial class SecureLogsLog
{
    [LoggerMessage(EventId = 2340, Level = LogLevel.Information, Message = "Secure logs for the diagnostics package: {Result} (exit code {ExitCode})")]
    public static partial void SecureLogsRequested(this ILogger logger, string result, int exitCode);

    [LoggerMessage(EventId = 2341, Level = LogLevel.Error, Message = "Starting the secure log copy failed")]
    public static partial void SecureLogsStartFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2342, Level = LogLevel.Warning, Message = "Secure logs for the diagnostics package not copied: {Result} (exit code {ExitCode})")]
    public static partial void SecureLogsNotCopied(this ILogger logger, string result, int exitCode);
}
