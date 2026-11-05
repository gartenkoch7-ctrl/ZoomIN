using Microsoft.Extensions.Logging;

namespace ZoomIN.Platform.SecureDesktop;

/// <summary>Log messages of the secure-desktop support (event ids 2300–2399).</summary>
internal static partial class SecureDesktopLog
{
    [LoggerMessage(EventId = 2301, Level = LogLevel.Information, Message = "Ease of Access notified ({Signal})")]
    public static partial void EaseOfAccessNotified(this ILogger logger, string signal);

    [LoggerMessage(EventId = 2302, Level = LogLevel.Warning, Message = "Ease of Access not notified ({Error})")]
    public static partial void EaseOfAccessNotNotified(this ILogger logger, string error);

    [LoggerMessage(EventId = 2303, Level = LogLevel.Information, Message = "Secure desktop support {State}")]
    public static partial void SupportChanged(this ILogger logger, string state);

    [LoggerMessage(EventId = 2304, Level = LogLevel.Error, Message = "Registering for the secure desktop failed")]
    public static partial void RegisterFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2305, Level = LogLevel.Error, Message = "Removing the secure desktop support failed")]
    public static partial void RemoveFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2306, Level = LogLevel.Information, Message = "Secure desktop: using {Source} settings")]
    public static partial void SettingsSource(this ILogger logger, string source);

    [LoggerMessage(EventId = 2307, Level = LogLevel.Warning, Message = "Secure desktop: the session user's settings are missing, refused or invalid")]
    public static partial void UserSettingsUnusable(this ILogger logger);

    [LoggerMessage(EventId = 2308, Level = LogLevel.Warning, Message = "Secure desktop: settings copy not written ({Error})")]
    public static partial void CopyNotWritten(this ILogger logger, string error);

    [LoggerMessage(EventId = 2309, Level = LogLevel.Warning, Message = "Secure desktop: session id unknown (error {Error})")]
    public static partial void SessionUnknown(this ILogger logger, int error);

    [LoggerMessage(EventId = 2310, Level = LogLevel.Information, Message = "Secure desktop: the user's AppData is not on a local fixed disk")]
    public static partial void AppDataNotLocal(this ILogger logger);

    [LoggerMessage(EventId = 2311, Level = LogLevel.Warning, Message = "Secure desktop: the user's AppData is unknown (0x{Result:X8})")]
    public static partial void AppDataUnknown(this ILogger logger, int result);

    [LoggerMessage(EventId = 2312, Level = LogLevel.Information, Message = "Secure desktop: no signed-in user in session {Session} (error {Error})")]
    public static partial void NoSignedInUser(this ILogger logger, uint session, int error);

    [LoggerMessage(EventId = 2313, Level = LogLevel.Information, Message = "Secure desktop: no user shell in session {Session}")]
    public static partial void NoUserShell(this ILogger logger, uint session);

    [LoggerMessage(EventId = 2314, Level = LogLevel.Information, Message = "Secure desktop: using the token of the user's shell")]
    public static partial void UsingShellToken(this ILogger logger);
}
