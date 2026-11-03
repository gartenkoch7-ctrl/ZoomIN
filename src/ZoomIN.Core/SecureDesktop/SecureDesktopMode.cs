namespace ZoomIN.Core.SecureDesktop;

/// <summary>Command line and give-up rule of the secure-desktop instance (ADR-0016).</summary>
public static class SecureDesktopMode
{
    /// <summary>"ZoomIN.exe --secure-desktop".</summary>
    public const string Switch = "--secure-desktop";

    /// <summary>
    /// After the watchdog gave up (<see cref="Lifecycle.RestartPolicy"/>), ZoomIN stays off on secure
    /// desktops until Windows restarts, so a crash loop can never stand between the user and sign-in.
    /// </summary>
    /// <param name="gaveUpAt">When the watchdog last gave up, or null if never.</param>
    /// <param name="bootTime">When Windows started.</param>
    public static bool IsDisabledAfterCrashes(DateTimeOffset? gaveUpAt, DateTimeOffset bootTime) =>
        gaveUpAt is { } time && time >= bootTime;
}
