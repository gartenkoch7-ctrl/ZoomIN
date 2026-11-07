using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Folders;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Watchdog.Crashes;
using ZoomIN.Watchdog.Notification;

namespace ZoomIN.Watchdog;

/// <summary>
/// Waits for the ZoomIN process to end. Exit code 0 is a regular shutdown; anything else
/// (crash, killed in Task Manager) triggers cleanup and a restart within the limits of
/// <see cref="RestartPolicy"/> (ADR-0007).
/// </summary>
public static class WatchdogRunner
{
    /// <param name="secure">Watches the secure-desktop instance (ADR-0016): restarts it with its switch, never notifies.</param>
    /// <param name="folders">Where the crash log and the give-up mark are kept (ADR-0025).</param>
    public static int Run(
        int processId, string appPath, IAppFolders folders, ILogger log, IReadOnlyList<ICleanupStep> cleanup, bool secure = false)
    {
        int exitCode;
        try
        {
            using var process = Process.GetProcessById(processId);

            // Touch the handle now: the exit code is only readable through a handle opened before exit.
            _ = process.Handle;
            process.WaitForExit();
            exitCode = process.ExitCode;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            log.CannotWatch(processId, ex.Message);
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
            Thread.Sleep(500);
        }

        log.UnexpectedExit(exitCode);
        RunCleanup(cleanup, log);

        var crashes = new CrashHistory(folders.CrashLog()).Record(DateTimeOffset.Now);
        if (RestartPolicy.ShouldRestart(crashes, DateTimeOffset.Now))
        {
            log.Restarting();
            // Through the shell, as CreateProcess cannot start the uiAccess build (ADR-0012).
            var arguments = TraceParentArgument.Append(secure ? SecureDesktopMode.Switch : string.Empty);
            Process.Start(new ProcessStartInfo(appPath, arguments) { UseShellExecute = true })?.Dispose();
            return 0;
        }

        log.GivingUp(RestartPolicy.MaxRestarts, RestartPolicy.Window.TotalMinutes);
        if (secure)
        {
            // No notification on a secure desktop; stay off there until Windows restarts.
            new SecureDesktopGiveUp(folders).Mark();
            return 2;
        }

        TrayNotification.Show(
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
