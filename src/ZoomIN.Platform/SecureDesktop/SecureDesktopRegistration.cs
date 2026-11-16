using Microsoft.Win32;
using ZoomIN.Core.Updates;

namespace ZoomIN.Platform.SecureDesktop;

/// <summary>
/// Registers ZoomIN with Windows' Ease of Access (ADR-0016; values from Microsoft's "Ease of Access –
/// Assistive Technology Registration"). Writes HKLM, so it only runs in elevated steps. The user's
/// choice is kept machine-wide, so updates keep "off" off.
/// <para>
/// Since ADR-0030 the AT entry "ATs\ZoomIN" stays while ZoomIN is installed, because the autostart
/// after sign-in (<c>EaseOfAccessAutostart</c>) needs it. The switch only decides the secure
/// desktops: on puts ZoomIN into the HKLM list (sign-in screen); off takes it out and sets
/// SecureDesktopAccommodation=none, so Windows starts nothing on the lock screen, Ctrl+Alt+Del or
/// UAC although ZoomIN runs as an AT.
/// </para>
/// </summary>
public sealed class SecureDesktopRegistration
{
    public const string AtName = "ZoomIN";

    /// <summary>The AT that runs on secure desktops instead; <see cref="NoAccommodation"/> for none.</summary>
    public const string AccommodationValue = "SecureDesktopAccommodation";

    public const string NoAccommodation = "none";

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

    /// <summary>The secure desktops are on: AT entry, in the HKLM list and no "none" accommodation.</summary>
    public bool IsRegistered
    {
        get
        {
            using var at = _machine.OpenSubKey(AtKeyPath);
            return at is not null
                && SignInList.Contains(AtName)
                && !NoAccommodation.Equals(at.GetValue(AccommodationValue) as string, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>The AT entry exists, whatever the switch says: the autostart list can use it (ADR-0030).</summary>
    public bool IsAtRegistered
    {
        get
        {
            using var at = _machine.OpenSubKey(AtKeyPath);
            return at is not null;
        }
    }

    private static string AtKeyPath => AtStartList.KeyPath + @"\ATs\" + AtName;

    // HKLM "Configuration": the ATs Windows starts on the sign-in screen.
    private AtStartList SignInList => new(_machine);

    /// <summary>Registers and switches according to <paramref name="choice"/>, else the kept preference (default on).</summary>
    /// <returns>Whether the secure desktops are now on.</returns>
    public bool Apply(InstallLayout layout, bool? choice)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (choice is { } chosen)
        {
            using var key = _machine.CreateSubKey(PreferencePath);
            key.SetValue(PreferenceValue, chosen ? 1 : 0, RegistryValueKind.DWord);
        }

        var enabled = choice ?? Preference ?? true;
        Register(layout, enabled);
        return enabled;
    }

    /// <summary>Removes the registration and the kept preference (uninstall).</summary>
    public void RemoveAll()
    {
        _machine.DeleteSubKeyTree(AtKeyPath, throwOnMissingSubKey: false);
        SignInList.Set(AtName, listed: false);
        _machine.DeleteSubKeyTree(PreferencePath, throwOnMissingSubKey: false);
    }

    private void Register(InstallLayout layout, bool secureDesktops)
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
            if (secureDesktops)
            {
                at.DeleteValue(AccommodationValue, throwOnMissingValue: false);
            }
            else
            {
                at.SetValue(AccommodationValue, NoAccommodation);
            }
        }

        SignInList.Set(AtName, secureDesktops);
    }
}
