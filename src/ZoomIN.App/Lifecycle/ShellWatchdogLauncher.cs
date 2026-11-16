using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Watchdog;

namespace ZoomIN.App.Lifecycle;

/// <summary>Starts this exe a second time in watchdog mode, watching the current process.</summary>
internal sealed class ShellWatchdogLauncher(ILogger<ShellWatchdogLauncher> log) : IWatchdogLauncher
{
    public int? Start(bool secure = false)
    {
        // While debugging, stopping the debugger must not trigger a restart.
        if (Debugger.IsAttached)
        {
            log.WatchdogSkipped();
            return null;
        }

        try
        {
            // Through the shell: CreateProcess refuses exes with uiAccess="true" (installer build,
            // ADR-0012) with ERROR_ELEVATION_REQUIRED; ShellExecute lets Windows grant UIAccess.
            var info = new ProcessStartInfo(Environment.ProcessPath!, TraceParentArgument.Append(WatchdogArguments.Format(Environment.ProcessId, secure)))
            {
                UseShellExecute = true,
            };
            using var watchdog = Process.Start(info);
            log.WatchdogStarted();
            return IdOf(watchdog);
        }
        catch (Win32Exception ex)
        {
            // ZoomIN still works without the watchdog, just without crash recovery.
            log.WatchdogFailed(ex);
            return null;
        }
    }

    // Only for the secure checks: a watchdog whose id the shell does not hand out still runs.
    private static int? IdOf(Process? watchdog)
    {
        try
        {
            return watchdog?.Id;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
