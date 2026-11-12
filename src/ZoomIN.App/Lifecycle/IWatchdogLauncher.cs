namespace ZoomIN.App.Lifecycle;

/// <summary>Starts the watchdog that restarts ZoomIN after a crash (ADR-0007).</summary>
internal interface IWatchdogLauncher
{
    /// <param name="secure">The current process is the secure-desktop instance (ADR-0016).</param>
    /// <returns>The watchdog's process id, or null if none was started (for the secure checks, M10).</returns>
    int? Start(bool secure = false);
}
