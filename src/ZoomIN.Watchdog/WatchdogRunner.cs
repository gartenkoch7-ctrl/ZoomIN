using System.ComponentModel;
using System.Diagnostics;
using ZoomIN.Core.Diagnostics;
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
    public static int Run(int processId, string appPath, ILog log, IReadOnlyList<ICleanupStep> cleanup, bool secure = false)
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
            log.Warn($"[Watchdog] Cannot watch process {processId}: {ex.Message}");
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

        log.Error($"[Watchdog] ZoomIN ended unexpectedly (exit code 0x{exitCode:X8}).");
        RunCleanup(cleanup, log);

        var crashes = CrashHistory.Default.Record(DateTimeOffset.Now);
        if (RestartPolicy.ShouldRestart(crashes, DateTimeOffset.Now))
        {
            log.Info("[Watchdog] Restarting ZoomIN.");
            // Through the shell, as CreateProcess cannot start the uiAccess build (ADR-0012).
            Process.Start(new ProcessStartInfo(appPath, secure ? SecureDesktopMode.Switch : "") { UseShellExecute = true })?.Dispose();
            return 0;
        }

        log.Error($"[Watchdog] Giving up after {RestartPolicy.MaxRestarts} restarts within {RestartPolicy.Window.TotalMinutes:0} minutes.");
        if (secure)
        {
            // No notification on a secure desktop; stay off there until Windows restarts.
            SecureDesktopGiveUp.Mark();
            return 2;
        }

        TrayNotification.Show(
            "ZoomIN wurde beendet",
            "ZoomIN ist wiederholt abgestürzt und wird nicht mehr neu gestartet. Details im Log unter %LocalAppData%\\ZoomIN\\logs.",
            TimeSpan.FromSeconds(10));
        return 2;
    }

    private static void RunCleanup(IReadOnlyList<ICleanupStep> cleanup, ILog log)
    {
        foreach (var step in cleanup)
        {
            try
            {
                step.Run();
                log.Info($"[Watchdog] Cleanup '{step.Name}' done.");
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // One failing step must not prevent the others or the restart.
                log.Error($"[Watchdog] Cleanup '{step.Name}' failed.", ex);
            }
        }
    }
}
