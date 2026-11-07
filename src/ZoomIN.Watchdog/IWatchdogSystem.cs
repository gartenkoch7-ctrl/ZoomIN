namespace ZoomIN.Watchdog;

/// <summary>
/// What <see cref="WatchdogRunner"/> needs from Windows: waiting for the process, starting it
/// again and the tray notice. Behind an interface so the restart rules can be tested with a fake
/// (M14 step 11); the static callbacks of the notice stay where they are.
/// </summary>
public interface IWatchdogSystem
{
    /// <summary>Waits until the process ends.</summary>
    /// <returns>False with a reason if the process cannot be watched (already gone, no access).</returns>
    bool TryWaitForExit(int processId, out int exitCode, out string reason);

    /// <summary>Starts ZoomIN again through the shell (ADR-0012: CreateProcess cannot start the uiAccess build).</summary>
    void Restart(string appPath, string arguments);

    /// <summary>Shows a tray notice for <paramref name="duration"/> and returns after it.</summary>
    void Notify(string title, string message, TimeSpan duration);

    void Pause(TimeSpan duration);
}
