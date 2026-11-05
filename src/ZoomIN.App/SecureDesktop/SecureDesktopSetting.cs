using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.SecureDesktop;

namespace ZoomIN.App.SecureDesktop;

/// <summary>
/// The "also on the sign-in screen" switch (ADR-0016). Reading works as the user; changing writes
/// HKLM, so the settings page starts the installed ZoomIN.exe elevated with
/// "--secure-desktop-setting on|off".
/// </summary>
internal static class SecureDesktopSetting
{
    // ERROR_CANCELLED: the user said "No" at the administrator prompt.
    private const int ErrorCancelled = 1223;

    /// <summary>Only an installation in Program Files can be registered (not dev builds or the ZIP).</summary>
    public static bool IsAvailable => InstallLayout.TryFromExecutable(Environment.ProcessPath, out _);

    public static bool IsEnabled => SecureDesktopRegistration.Default.IsRegistered;

    public static bool TryParse(IReadOnlyList<string> args, out bool on)
    {
        on = args is [SecureDesktopMode.SettingSwitch, "on"];
        return on || args is [SecureDesktopMode.SettingSwitch, "off"];
    }

    /// <summary>The elevated step: registers or removes, and keeps the choice for updates.</summary>
    public static int RunElevated(bool on, ILogger log)
    {
        if (!InstallLayout.TryFromExecutable(Environment.ProcessPath, out var layout))
        {
            return 1;
        }

        try
        {
            SecureDesktopRegistration.Default.Apply(layout, on);
            log.SupportSwitched(on ? "on" : "off");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            log.ChangeFailed(ex);
            return 1;
        }
    }

    /// <summary>Asks for administrator rights and switches. Blocks until done; call off the UI thread.</summary>
    /// <returns>True if the switch now has the requested state.</returns>
    public static bool RequestChange(bool on, ILogger log)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(
                Environment.ProcessPath!, TraceParentArgument.Append($"{SecureDesktopMode.SettingSwitch} {(on ? "on" : "off")}"))
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
            log.SwitchStartFailed(ex);
            return false;
        }
    }
}
