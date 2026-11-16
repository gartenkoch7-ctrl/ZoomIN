using System.Globalization;

namespace ZoomIN.Core.Updates;

/// <summary>
/// The entry in Windows "Apps" (HKLM\...\Uninstall\ZoomIN) of an installation (ADR-0014/0015).
/// Everything points at the "current" link, so an update only has to refresh the version.
/// </summary>
public sealed record UninstallEntry(string DisplayVersion, string InstallLocation, string DisplayIcon, string UninstallString)
{
    public const string KeyName = "ZoomIN";
    public const string DisplayName = "ZoomIN";
    public const string Publisher = "ZoomIN contributors";

    /// <summary>"ZoomIN.exe --uninstall" asks about the settings, then removes the installation.</summary>
    public const string UninstallSwitch = "--uninstall";

    /// <summary>The elevated part of the removal; never started by the user directly.</summary>
    public const string RemoveSwitch = "--remove-installation";

    public static UninstallEntry For(InstallLayout layout, AppVersion version)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(version);
        var exe = layout.CurrentExecutable;
        return new UninstallEntry(
            version.ToString(),
            layout.Root,
            exe,
            string.Create(CultureInfo.InvariantCulture, $"\"{exe}\" {UninstallSwitch}"));
    }
}
