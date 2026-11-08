using Microsoft.Win32;
using ZoomIN.Core.Shell;
using ZoomIN.Core.Updates;

namespace ZoomIN.Platform.Updates;

/// <summary>The uninstall entry in HKLM and the Start menu shortcut for all users (ADR-0014).</summary>
public sealed class WindowsInstallRegistration(IShortcuts shortcuts) : IInstallRegistration
{
    private const string UninstallRoot = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\";

    public void Register(InstallLayout layout, AppVersion version)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var entry = UninstallEntry.For(layout, version);
        using var key = Registry.LocalMachine.CreateSubKey(UninstallRoot + UninstallEntry.KeyName);
        key.SetValue("DisplayName", UninstallEntry.DisplayName);
        key.SetValue("DisplayVersion", entry.DisplayVersion);
        key.SetValue("Publisher", UninstallEntry.Publisher);
        key.SetValue("InstallLocation", entry.InstallLocation);
        key.SetValue("DisplayIcon", entry.DisplayIcon);
        key.SetValue("UninstallString", entry.UninstallString);
        key.SetValue("URLInfoAbout", "https://github.com/gartenkoch7-ctrl/ZoomIN");

        // Channel and version are chosen in ZoomIN itself; "Modify"/"Repair" would have nothing to do.
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);

        shortcuts.Create(shortcuts.CommonStartMenu, layout.CurrentExecutable);
    }

    public void Unregister()
    {
        Registry.LocalMachine.DeleteSubKeyTree(UninstallRoot + UninstallEntry.KeyName, throwOnMissingSubKey: false);
        shortcuts.Remove(shortcuts.CommonStartMenu);
    }

    public bool RemoveLegacyInstall(TimeSpan timeout)
    {
        using var key = Registry.CurrentUser.OpenSubKey(UninstallRoot + LegacyInstall.InnoUninstallKey);
        var uninstaller = LegacyInstall.UninstallerPath(key?.GetValue("UninstallString") as string);
        if (uninstaller is null || !File.Exists(uninstaller))
        {
            return false;
        }

        using var process = System.Diagnostics.Process.Start(uninstaller, LegacyInstall.SilentArguments);
        process.WaitForExit(timeout);
        return true;
    }
}
