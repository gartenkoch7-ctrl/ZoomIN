using ZoomIN.Core.SecureDesktop;

namespace ZoomIN.App.SecureDesktop;

/// <summary>The command line of the elevated switch step: "--secure-desktop-setting on|off" (ADR-0016).</summary>
internal static class SecureDesktopSetting
{
    public static bool TryParse(IReadOnlyList<string> args, out bool on)
    {
        on = args is [SecureDesktopMode.SettingSwitch, "on"];
        return on || args is [SecureDesktopMode.SettingSwitch, "off"];
    }
}
