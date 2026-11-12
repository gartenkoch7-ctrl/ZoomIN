using Microsoft.Extensions.Logging;

namespace ZoomIN.Platform.Lifecycle;

/// <summary>Log messages of the autostart and of leaving the Ease of Access job (event ids 2320–2339, ADR-0030).</summary>
internal static partial class AutostartLog
{
    [LoggerMessage(EventId = 2320, Level = LogLevel.Information, Message = "Autostart moved from the Run key to the Ease of Access list")]
    public static partial void AutostartMigrated(this ILogger logger);

    [LoggerMessage(EventId = 2321, Level = LogLevel.Information, Message = "Autostart migration postponed: ZoomIN is not registered with Ease of Access yet")]
    public static partial void MigrationPostponed(this ILogger logger);

    [LoggerMessage(EventId = 2322, Level = LogLevel.Warning, Message = "Autostart migration failed ({Error})")]
    public static partial void MigrationFailed(this ILogger logger, string error);

    [LoggerMessage(EventId = 2323, Level = LogLevel.Information,
        Message = "Launch origin: in job {InJob}, job limits 0x{JobLimits:X8}, parent {ParentProcess}, left job {LeftJob}, Ease of Access start {EaseOfAccessStart}")]
    public static partial void LaunchOriginFound(this ILogger logger, bool inJob, uint jobLimits, string parentProcess, bool leftJob, bool easeOfAccessStart);

    [LoggerMessage(EventId = 2324, Level = LogLevel.Information, Message = "Leaving the Ease of Access job: started process {ProcessId} ({Method})")]
    public static partial void LeftJob(this ILogger logger, int processId, string method);

    [LoggerMessage(EventId = 2325, Level = LogLevel.Warning,
        Message = "Cannot leave job (job limits 0x{JobLimits:X8}): the tray app ends at the next desktop switch")]
    public static partial void CannotLeaveJob(this ILogger logger, uint jobLimits);

    [LoggerMessage(EventId = 2326, Level = LogLevel.Warning, Message = "Leaving the job failed (error {Error}); staying in the job")]
    public static partial void LeaveJobFailed(this ILogger logger, int error);

    [LoggerMessage(EventId = 2327, Level = LogLevel.Warning, Message = "Still in a job after leaving the Ease of Access job (job limits 0x{JobLimits:X8})")]
    public static partial void StillInJob(this ILogger logger, uint jobLimits);

    [LoggerMessage(EventId = 2328, Level = LogLevel.Information, Message = "Started by Ease of Access: no Win+U signal")]
    public static partial void NotifierNotNeeded(this ILogger logger);

    [LoggerMessage(EventId = 2329, Level = LogLevel.Information, Message = "Autostart switched {State} ({Method})")]
    public static partial void AutostartSwitched(this ILogger logger, string state, string method);
}
