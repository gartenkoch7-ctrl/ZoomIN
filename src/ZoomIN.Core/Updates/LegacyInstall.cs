namespace ZoomIN.Core.Updates;

/// <summary>
/// The per-user Inno Setup installation of ZoomIN up to M8 (ADR-0009), replaced by the setup
/// program (ADR-0014). Its uninstaller is run silently before installing.
/// </summary>
public static class LegacyInstall
{
    /// <summary>HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\&lt;this&gt; (AppId + "_is1").</summary>
    public const string InnoUninstallKey = "{8C6F0E5A-2B7D-4F3B-9C1E-5A7D2E4B6F10}_is1";

    /// <summary>No dialogs, no restart; ZoomIN's own data stays (the Inno uninstaller never removed it).</summary>
    public const string SilentArguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART";

    /// <summary>Extracts the executable from an Inno "UninstallString" such as "\"C:\...\unins000.exe\"".</summary>
    public static string? UninstallerPath(string? uninstallString)
    {
        var value = uninstallString?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (value[0] == '"')
        {
            var end = value.IndexOf('"', 1);
            return end > 1 ? value[1..end] : null;
        }

        var space = value.IndexOf(' ', StringComparison.Ordinal);
        return space > 0 ? value[..space] : value;
    }
}
