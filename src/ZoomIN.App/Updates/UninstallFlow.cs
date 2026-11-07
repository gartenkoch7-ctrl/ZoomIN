using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Folders;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Core.Localization;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Shell;
using ZoomIN.Core.Updates;

namespace ZoomIN.App.Updates;

/// <summary>
/// "ZoomIN.exe --uninstall" from Windows "Apps" (ADR-0014): asks whether settings stay, runs the
/// elevated removal, then cleans up the signed-in user's own traces.
/// </summary>
internal sealed class UninstallFlow(
    IMessageDialog dialog, IShortcuts shortcuts, IInstallRegistration registration, ISecureDesktopInstall secureDesktop,
    IRunningInstall install, IRemovalSteps removal, IAutostart autostart, IAppFolders folders, ILocalizer texts,
    ILogger<UninstallFlow> log)
{
    public int Run()
    {
        texts.SetLanguage(AppLanguage.System);
        var title = texts.Get("Uninstall.Title");
        if (!install.TryGetLayout(out _))
        {
            return (int)UpdateResult.NotInstalled;
        }

        var answer = dialog.AskYesNoCancel(title, texts.Get("Uninstall.Question"));
        if (answer == DialogAnswer.Cancel)
        {
            return 0;
        }

        if (!removal.RunElevated())
        {
            dialog.Inform(title, texts.Get("Uninstall.Failed"), error: true);
            return (int)UpdateResult.Failed;
        }

        TryRun(() => autostart.SetEnabled(false));
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
        if (!install.TryGetLayout(out var layout))
        {
            return (int)UpdateResult.NotInstalled;
        }

        removal.EndOtherInstances(waitForProcessId);
        registration.Unregister();
        secureDesktop.Remove();
        removal.ScheduleFolderRemoval(layout, waitForProcessId);
        log.RemovalScheduled();
        return 0;
    }

    private static void TryRun(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception
            or System.Security.SecurityException)
        {
            // Best effort: leftovers in the user's own folders do no harm.
        }
    }
}
