using ZoomIN.App.Updates;
using ZoomIN.Core.Folders;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Core.Shell;
using ZoomIN.Core.Updates;

namespace ZoomIN.App.Tests.Updates;

/// <summary>Fakes for the uninstall and update flows: they only record what was asked of them.</summary>
internal sealed class UninstallFakes
{
    public List<string> Calls { get; } = [];

    public DialogAnswer Answer { get; set; } = DialogAnswer.Yes;

    public bool ElevatedSucceeds { get; set; } = true;

    /// <summary>Autostart and desktop shortcut throw, as a locked registry key or a busy file would.</summary>
    public bool UserCleanupFails { get; set; }

    public InstallLayout? Layout { get; set; } = new(@"C:\Program Files\ZoomIN");

    public sealed class Dialog(UninstallFakes calls) : IMessageDialog
    {
        public DialogAnswer AskYesNoCancel(string title, string text)
        {
            calls.Calls.Add("ask");
            return calls.Answer;
        }

        public void Inform(string title, string text, bool error = false) => calls.Calls.Add(error ? "inform-error" : "inform");
    }

    public sealed class Shortcuts(UninstallFakes calls) : IShortcuts
    {
        public string CommonStartMenu => "start-menu";

        public string UserDesktop => "desktop";

        public void Create(string shortcutPath, string targetPath) => calls.Calls.Add("shortcut-create " + shortcutPath);

        public void Remove(string shortcutPath)
        {
            calls.Calls.Add("shortcut-remove " + shortcutPath);
            if (calls.UserCleanupFails)
            {
                throw new IOException("shortcut in use");
            }
        }
    }

    public sealed class Registration(UninstallFakes calls) : IInstallRegistration
    {
        public Exception? RegisterFails { get; set; }

        public void Register(InstallLayout layout, AppVersion version)
        {
            calls.Calls.Add("register " + version);
            if (RegisterFails is { } ex)
            {
                throw ex;
            }
        }

        public void Unregister() => calls.Calls.Add("unregister");

        public bool RemoveLegacyInstall(TimeSpan timeout) => false;
    }

    public sealed class SecureDesktop(UninstallFakes calls) : ISecureDesktopInstall
    {
        public void Apply(InstallLayout layout, bool? choice) => calls.Calls.Add("secure-apply " + (choice?.ToString() ?? "keep"));

        public void Remove() => calls.Calls.Add("secure-remove");
    }

    public sealed class Install(UninstallFakes calls) : IRunningInstall
    {
        public bool TryGetLayout(out InstallLayout layout)
        {
            layout = calls.Layout ?? new InstallLayout(string.Empty);
            return calls.Layout is not null;
        }
    }

    public sealed class Removal(UninstallFakes calls) : IRemovalSteps
    {
        public bool RunElevated()
        {
            calls.Calls.Add("elevated");
            return calls.ElevatedSucceeds;
        }

        public void EndOtherInstances(int waitForProcessId) => calls.Calls.Add("end-others " + waitForProcessId);

        public void ScheduleFolderRemoval(InstallLayout layout, int waitForProcessId) =>
            calls.Calls.Add($"schedule-removal {layout.Root} {waitForProcessId}");
    }

    public sealed class Autostart(UninstallFakes calls) : IAutostart
    {
        public bool IsEnabled => false;

        public void SetEnabled(bool enabled)
        {
            calls.Calls.Add("autostart " + enabled);
            if (calls.UserCleanupFails)
            {
                throw new System.Security.SecurityException("registry");
            }
        }
    }

    public sealed record Folders(string Root) : IAppFolders
    {
        public string Local => Path.Combine(Root, "local");

        public string Roaming => Path.Combine(Root, "roaming");
    }
}
