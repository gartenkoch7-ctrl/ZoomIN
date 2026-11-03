using Microsoft.Win32;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Shell;

namespace ZoomIN.Platform.Updates;

/// <summary>
/// Machine-wide traces of an installation (ADR-0014): the entry in Windows "Apps" and the Start
/// menu shortcut for all users. Needs administrator rights, so it runs in the elevated step.
/// </summary>
public static class InstallRegistration
{
    private const string UninstallRoot = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\";

    public static void Register(InstallLayout layout, AppVersion version)
    {
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

        ShellShortcut.Create(ShellShortcut.CommonStartMenu, layout.CurrentExecutable);
    }

    public static void Unregister()
    {
        Registry.LocalMachine.DeleteSubKeyTree(UninstallRoot + UninstallEntry.KeyName, throwOnMissingSubKey: false);
        ShellShortcut.Remove(ShellShortcut.CommonStartMenu);
    }

    /// <summary>Silently removes the old per-user Inno Setup installation (M8), if there is one.</summary>
    /// <returns>True if one was found and its uninstaller ran.</returns>
    public static bool RemoveLegacyInstall(TimeSpan timeout)
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
