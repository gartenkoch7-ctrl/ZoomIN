using Microsoft.Win32;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Core.Updates;

namespace ZoomIN.Platform.SecureDesktop;

/// <summary>
/// Registers ZoomIN with Windows' Ease of Access so Windows starts it on the sign-in screen, lock
/// screen, Ctrl+Alt+Del and UAC (ADR-0016; values from Microsoft's "Ease of Access – Assistive
/// Technology Registration"). Writes HKLM, so it only runs in elevated steps. The user's choice is
/// kept machine-wide, so updates keep "off" off.
/// </summary>
public sealed class SecureDesktopRegistration
{
    public const string AtName = "ZoomIN";

    private const string AccessibilityPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Accessibility";
    private const string PreferencePath = @"SOFTWARE\ZoomIN";
    private const string PreferenceValue = "SecureDesktop";

    private readonly RegistryKey _machine;

    /// <param name="machine">HKLM; tests pass a private key instead.</param>
    public SecureDesktopRegistration(RegistryKey machine)
    {
        _machine = machine;
    }

    public static SecureDesktopRegistration Default => new(Registry.LocalMachine);

    /// <summary>The user's last choice, or null if none was made (then the default is on).</summary>
    public bool? Preference
    {
        get
        {
            using var key = _machine.OpenSubKey(PreferencePath);
            return key?.GetValue(PreferenceValue) is int value ? value != 0 : null;
        }
    }

    public bool IsRegistered
    {
        get
        {
            using var at = _machine.OpenSubKey(AtKeyPath);
            using var access = _machine.OpenSubKey(AccessibilityPath);
            return at is not null && AtConfigurationList.Contains(access?.GetValue("Configuration") as string, AtName);
        }
    }

    private static string AtKeyPath => AccessibilityPath + @"\ATs\" + AtName;

    /// <summary>Registers or removes according to <paramref name="choice"/>, else the kept preference (default on).</summary>
    /// <returns>Whether ZoomIN is now registered.</returns>
    public bool Apply(InstallLayout layout, bool? choice)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (choice is { } chosen)
        {
            using var key = _machine.CreateSubKey(PreferencePath);
            key.SetValue(PreferenceValue, chosen ? 1 : 0, RegistryValueKind.DWord);
        }

        var enabled = choice ?? Preference ?? true;
        if (enabled)
        {
            Register(layout);
        }
        else
        {
            Unregister();
        }

        return enabled;
    }

    /// <summary>Removes the registration and the kept preference (uninstall).</summary>
    public void RemoveAll()
    {
        Unregister();
        _machine.DeleteSubKeyTree(PreferencePath, throwOnMissingSubKey: false);
    }

    private void Register(InstallLayout layout)
    {
        using (var at = _machine.CreateSubKey(AtKeyPath))
        {
            at.SetValue("ApplicationName", "ZoomIN");
            at.SetValue("Description", "ZoomIN – Bildschirmlupe / screen magnifier");
            at.SetValue("ATExe", InstallLayout.ExecutableName);

            // Always the installed copy in Program Files: Windows starts it as SYSTEM, so it must
            // never point into a folder a user could write to. No arguments: ZoomIN recognises the
            // secure desktop itself because it runs as SYSTEM there.
            at.SetValue("StartExe", layout.CurrentExecutable);
            at.SetValue("Profile", """<HCIModel><Accommodation type="mild vision" /><Accommodation type="severe vision" /></HCIModel>""");
            at.SetValue("SimpleProfile", "Magnifier");

            // Default behaviour, written explicitly: a fresh instance per secure desktop, ended on the way back.
            at.SetValue("TerminateOnDesktopSwitch", 1, RegistryValueKind.DWord);
        }

        using var access = _machine.CreateSubKey(AccessibilityPath);
        access.SetValue("Configuration", AtConfigurationList.Add(access.GetValue("Configuration") as string, AtName));
    }

    private void Unregister()
    {
        _machine.DeleteSubKeyTree(AtKeyPath, throwOnMissingSubKey: false);
        using var access = _machine.OpenSubKey(AccessibilityPath, writable: true);
        if (access?.GetValue("Configuration") is string list && AtConfigurationList.Contains(list, AtName))
        {
            access.SetValue("Configuration", AtConfigurationList.Remove(list, AtName));
        }
    }
}
