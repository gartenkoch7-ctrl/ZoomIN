using System.ComponentModel;
using System.Diagnostics;
using ZoomIN.Watchdog.Notification;

namespace ZoomIN.Watchdog;

/// <summary>The watchdog's work with real processes and the tray.</summary>
public sealed class ProcessWatchdogSystem : IWatchdogSystem
{
    public bool TryWaitForExit(int processId, out int exitCode, out string reason)
    {
        exitCode = 0;
        reason = string.Empty;
        try
        {
            using var process = Process.GetProcessById(processId);

            // Touch the handle now: the exit code is only readable through a handle opened before exit.
            _ = process.Handle;
            process.WaitForExit();
            exitCode = process.ExitCode;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            reason = ex.Message;
            return false;
        }
    }

    public void Restart(string appPath, string arguments) =>
        Process.Start(new ProcessStartInfo(appPath, arguments) { UseShellExecute = true })?.Dispose();

    public void Notify(string title, string message, TimeSpan duration) => TrayNotification.Show(title, message, duration);

    public void Pause(TimeSpan duration) => Thread.Sleep(duration);
}
