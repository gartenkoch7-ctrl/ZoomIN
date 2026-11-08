using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.SecureDesktop;

namespace ZoomIN.App.SecureDesktop;

/// <summary>Registers ZoomIN for the secure desktops through <see cref="SecureDesktopRegistration"/> (HKLM).</summary>
internal sealed class SecureDesktopSwitch(ILogger<SecureDesktopSwitch> log) : ISecureDesktopSwitch
{
    // ERROR_CANCELLED: the user said "No" at the administrator prompt.
    private const int ErrorCancelled = 1223;

    public bool IsAvailable => InstallLayout.TryFromExecutable(Environment.ProcessPath, out _);

    public bool IsEnabled => SecureDesktopRegistration.Default.IsRegistered;

    public int ApplyElevated(bool on)
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

    public bool RequestChange(bool on)
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
