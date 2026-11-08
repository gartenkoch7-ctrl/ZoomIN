using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Folders;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Watchdog.Crashes;

namespace ZoomIN.Watchdog;

/// <summary>
/// Waits for the ZoomIN process to end. Exit code 0 is a regular shutdown; anything else
/// (crash, killed in Task Manager) triggers cleanup and a restart within the limits of
/// <see cref="RestartPolicy"/> (ADR-0007).
/// </summary>
/// <param name="folders">Where the crash log and the give-up mark are kept (ADR-0025).</param>
/// <param name="cleanup">What a crashed ZoomIN leaves behind and the watchdog gives back.</param>
/// <param name="system">Waiting, restarting and the notice; a fake in the tests.</param>
/// <param name="clock">Time of the crash for the restart limits.</param>
public sealed class WatchdogRunner(
    IAppFolders folders, ILogger log, IReadOnlyList<ICleanupStep> cleanup, IWatchdogSystem system, TimeProvider clock)
{
    /// <param name="secure">Watches the secure-desktop instance (ADR-0016): restarts it with its switch, never notifies.</param>
    public int Run(int processId, string appPath, bool secure = false)
    {
        if (!system.TryWaitForExit(processId, out var exitCode, out var reason))
        {
            log.CannotWatch(processId, reason);
            return 1;
        }

        if (exitCode == 0)
        {
            return 0;
        }

        if (secure)
        {
            // Windows ends the whole job (instance and watchdog) on every desktop switch. If this
            // watchdog is still alive a moment later, the instance really crashed.
            system.Pause(TimeSpan.FromMilliseconds(500));
        }

        log.UnexpectedExit(exitCode);
        RunCleanup(cleanup, log);

        var now = clock.GetLocalNow();
        var crashes = new CrashHistory(folders.CrashLog()).Record(now);
        if (RestartPolicy.ShouldRestart(crashes, now))
        {
            log.Restarting();
            system.Restart(appPath, TraceParentArgument.Append(secure ? SecureDesktopMode.Switch : string.Empty));
            return 0;
        }

        log.GivingUp(RestartPolicy.MaxRestarts, RestartPolicy.Window.TotalMinutes);
        if (secure)
        {
            // No notification on a secure desktop; stay off there until Windows restarts.
            new SecureDesktopGiveUp(folders, clock).Mark();
            return 2;
        }

        system.Notify(
            "ZoomIN wurde beendet",
            "ZoomIN ist wiederholt abgestürzt und wird nicht mehr neu gestartet. Details im Log unter %LocalAppData%\\ZoomIN\\logs.",
            TimeSpan.FromSeconds(10));
        return 2;
    }

    private static void RunCleanup(IReadOnlyList<ICleanupStep> cleanup, ILogger log)
    {
        foreach (var step in cleanup)
        {
            try
            {
                step.Run();
                log.CleanupDone(step.Name);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // One failing step must not prevent the others or the restart.
                log.CleanupFailed(ex, step.Name);
            }
        }
    }
}
