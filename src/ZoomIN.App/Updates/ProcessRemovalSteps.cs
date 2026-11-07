using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Updates;

namespace ZoomIN.App.Updates;

/// <summary>The uninstall's process work with real processes, UAC and PowerShell.</summary>
internal sealed class ProcessRemovalSteps(ILogger<ProcessRemovalSteps> log) : IRemovalSteps
{
    // ERROR_CANCELLED: the user said "No" at the administrator prompt.
    private const int ErrorCancelled = 1223;

    public bool RunElevated()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(
                Environment.ProcessPath!,
                TraceParentArgument.Append(string.Create(CultureInfo.InvariantCulture, $"{UninstallEntry.RemoveSwitch} {Environment.ProcessId}")))
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return false;
        }
        catch (Win32Exception ex)
        {
            log.RemovalFailed(ex);
            return false;
        }
    }

    public void EndOtherInstances(int waitForProcessId)
    {
        foreach (var process in Process.GetProcessesByName("ZoomIN").Where(p => p.Id != Environment.ProcessId && p.Id != waitForProcessId))
        {
            using (process)
            {
                try
                {
                    process.Kill();
                }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
                {
                    // Already gone, or not ours to end: the folder removal waits for the rest.
                }
            }
        }
    }

    public void ScheduleFolderRemoval(InstallLayout layout, int waitForProcessId)
    {
        // The running ZoomIN.exe files stay in use until both processes have ended.
        var script = string.Create(
            CultureInfo.InvariantCulture,
            $"Wait-Process -Id {waitForProcessId},{Environment.ProcessId} -ErrorAction SilentlyContinue; Start-Sleep 1; Remove-Item -LiteralPath '{layout.Root}' -Recurse -Force");
        using var cleanup = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -WindowStyle Hidden -Command \"{script}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        });
    }
}
