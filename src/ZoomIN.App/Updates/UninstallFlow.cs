using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Folders;
using ZoomIN.Core.Localization;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Shell;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Lifecycle;

namespace ZoomIN.App.Updates;

/// <summary>
/// "ZoomIN.exe --uninstall" from Windows "Apps" (ADR-0014): asks whether settings stay, runs the
/// elevated removal, then cleans up the signed-in user's own traces.
/// </summary>
internal sealed class UninstallFlow(
    IMessageDialog dialog, IShortcuts shortcuts, IInstallRegistration registration, ISecureDesktopInstall secureDesktop,
    IAppFolders folders, ILocalizer texts, ILogger<UninstallFlow> log)
{
    // ERROR_CANCELLED: the user said "No" at the administrator prompt.
    private const int ErrorCancelled = 1223;

    public int Run()
    {
        texts.SetLanguage(AppLanguage.System);
        var title = texts.Get("Uninstall.Title");
        if (!InstallLayout.TryFromExecutable(Environment.ProcessPath, out var layout))
        {
            return (int)UpdateResult.NotInstalled;
        }

        var answer = dialog.AskYesNoCancel(title, texts.Get("Uninstall.Question"));
        if (answer == DialogAnswer.Cancel)
        {
            return 0;
        }

        if (!RunElevatedRemoval())
        {
            dialog.Inform(title, texts.Get("Uninstall.Failed"), error: true);
            return (int)UpdateResult.Failed;
        }

        new RegistryAutostart(RegistryAutostart.RunKey, $"\"{layout.CurrentExecutable}\"").SetEnabled(false);
        TryRun(() => shortcuts.Remove(shortcuts.UserDesktop));
        if (answer == DialogAnswer.No)
        {
            TryRun(() => Directory.Delete(folders.Roaming, recursive: true));
            TryRun(() => Directory.Delete(folders.Local, recursive: true));
        }

        log.Uninstalled();
        dialog.Inform(title, texts.Get("Uninstall.Done"));
        return 0;
    }

    /// <summary>
    /// "--remove-installation &lt;pid&gt;", elevated: ends ZoomIN, removes the Apps entry and the
    /// Start menu shortcut, and deletes Program Files\ZoomIN once the asking process has ended
    /// (its own files are in use until then).
    /// </summary>
    public int RemoveElevated(int waitForProcessId)
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

        registration.Unregister();
        secureDesktop.Remove();
        var script = string.Create(
            CultureInfo.InvariantCulture,
            $"Wait-Process -Id {waitForProcessId},{Environment.ProcessId} -ErrorAction SilentlyContinue; Start-Sleep 1; Remove-Item -LiteralPath '{layout.Root}' -Recurse -Force");
        using var cleanup = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -WindowStyle Hidden -Command \"{script}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        });
        log.RemovalScheduled();
        return 0;
    }

    private bool RunElevatedRemoval()
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
