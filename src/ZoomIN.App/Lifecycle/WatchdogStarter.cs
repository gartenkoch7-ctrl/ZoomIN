using System.ComponentModel;
using System.Diagnostics;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Watchdog;

namespace ZoomIN.App.Lifecycle;

/// <summary>Starts this exe a second time in watchdog mode, watching the current process.</summary>
internal static class WatchdogStarter
{
    /// <param name="secure">The current process is the secure-desktop instance (ADR-0016).</param>
    public static void Start(ILog log, bool secure = false)
    {
        // While debugging, stopping the debugger must not trigger a restart.
        if (Debugger.IsAttached)
        {
            log.Info("Debugger attached: watchdog not started.");
            return;
        }

        try
        {
            // Through the shell: CreateProcess refuses exes with uiAccess="true" (installer build,
            // ADR-0012) with ERROR_ELEVATION_REQUIRED; ShellExecute lets Windows grant UIAccess.
            var info = new ProcessStartInfo(Environment.ProcessPath!, WatchdogArguments.Format(Environment.ProcessId, secure))
            {
                UseShellExecute = true,
            };
            Process.Start(info)?.Dispose();
            log.Info("Watchdog started.");
        }
        catch (Win32Exception ex)
        {
            // ZoomIN still works without the watchdog, just without crash recovery.
            log.Error("Starting the watchdog failed.", ex);
        }
    }
}
