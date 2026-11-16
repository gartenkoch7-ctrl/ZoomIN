using Microsoft.Extensions.Logging;

namespace ZoomIN.Setup.Install;

/// <summary>Log messages of the setup (event ids 5000–5099).</summary>
internal static partial class SetupLog
{
    [LoggerMessage(EventId = 5001, Level = LogLevel.Information, Message = "Setup: ZoomIN {ProcessId} not ended: {Reason}")]
    public static partial void ProcessNotEnded(this ILogger logger, int processId, string reason);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Error, Message = "Setup: registering the installation failed")]
    public static partial void RegistrationFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 5003, Level = LogLevel.Information, Message = "Setup: old Inno installation removed")]
    public static partial void InnoRemoved(this ILogger logger);
}
