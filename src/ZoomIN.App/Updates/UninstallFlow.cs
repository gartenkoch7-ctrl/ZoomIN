using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using ZoomIN.App.Localization;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Lifecycle;
using ZoomIN.Platform.Shell;
using ZoomIN.Platform.Updates;

namespace ZoomIN.App.Updates;

/// <summary>
/// "ZoomIN.exe --uninstall" from Windows "Apps" (ADR-0014): asks whether settings stay, runs the
/// elevated removal, then cleans up the signed-in user's own traces.
/// </summary>
internal static class UninstallFlow
{
    // ERROR_CANCELLED: the user said "No" at the administrator prompt.
    private const int ErrorCancelled = 1223;

    public static int Run(ILog log)
    {
        Text.SetLanguage(AppLanguage.System);
        var title = Text.Get("Uninstall.Title");
        if (!InstallLayout.TryFromExecutable(Environment.ProcessPath, out var layout))
        {
            return (int)UpdateResult.NotInstalled;
        }

        var answer = MessageDialog.AskYesNoCancel(title, Text.Get("Uninstall.Question"));
        if (answer == MessageDialog.Answer.Cancel)
        {
            return 0;
        }

        if (!RunElevatedRemoval(log))
        {
            MessageDialog.Inform(title, Text.Get("Uninstall.Failed"), error: true);
            return (int)UpdateResult.Failed;
        }

        new RegistryAutostart(RegistryAutostart.RunKey, $"\"{layout.CurrentExecutable}\"").SetEnabled(false);
        TryRun(() => ShellShortcut.Remove(ShellShortcut.UserDesktop));
        if (answer == MessageDialog.Answer.No)
        {
            TryRun(() => Directory.Delete(DataFolder(Environment.SpecialFolder.ApplicationData), recursive: true));
            TryRun(() => Directory.Delete(DataFolder(Environment.SpecialFolder.LocalApplicationData), recursive: true));
        }

        log.Info("ZoomIN uninstalled.");
        MessageDialog.Inform(title, Text.Get("Uninstall.Done"));
        return 0;
    }

    /// <summary>
    /// "--remove-installation &lt;pid&gt;", elevated: ends ZoomIN, removes the Apps entry and the
    /// Start menu shortcut, and deletes Program Files\ZoomIN once the asking process has ended
    /// (its own files are in use until then).
    /// </summary>
    public static int RemoveElevated(int waitForProcessId, ILog log)
    {
        if (!InstallLayout.TryFromExecutable(Environment.ProcessPath, out var layout))
        {
            return (int)UpdateResult.NotInstalled;
        }

        foreach (var process in Process.GetProcessesByName("ZoomIN").Where(p => p.Id != Environment.ProcessId && p.Id != waitForProcessId))
        {
            using (process)
            {
                TryRun(() => process.Kill());
            }
        }

        InstallRegistration.Unregister();
        var script = string.Create(
            CultureInfo.InvariantCulture,
            $"Wait-Process -Id {waitForProcessId},{Environment.ProcessId} -ErrorAction SilentlyContinue; Start-Sleep 1; Remove-Item -LiteralPath '{layout.Root}' -Recurse -Force");
        using var cleanup = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -WindowStyle Hidden -Command \"{script}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        });
        log.Info("[Uninstall] Removal scheduled.");
        return 0;
    }

    private static bool RunElevatedRemoval(ILog log)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(
                Environment.ProcessPath!,
                string.Create(CultureInfo.InvariantCulture, $"{UninstallEntry.RemoveSwitch} {Environment.ProcessId}"))
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
            log.Error("[Uninstall] Starting the removal failed", ex);
            return false;
        }
    }

    private static string DataFolder(Environment.SpecialFolder folder) => Path.Combine(Environment.GetFolderPath(folder), "ZoomIN");

    private static void TryRun(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            // Best effort: leftovers in the user's own folders do no harm.
        }
    }
}
