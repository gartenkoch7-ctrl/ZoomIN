using Microsoft.Extensions.Logging;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Core.Updates;

namespace ZoomIN.Platform.SecureDesktop;

/// <summary>Registers ZoomIN as assistive technology for the sign-in screen (ADR-0016), as administrator.</summary>
public sealed class SystemSecureDesktopInstall(ILogger<SystemSecureDesktopInstall> log) : ISecureDesktopInstall
{
    public void Apply(InstallLayout layout, bool? choice)
    {
        try
        {
            var on = SecureDesktopRegistration.Default.Apply(layout, choice);
            log.SupportChanged(on ? "registered" : "off");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            log.RegisterFailed(ex);
        }
    }

    public void Remove()
    {
        try
        {
            SecureDesktopRegistration.Default.RemoveAll();

            // The secure instance's files lie in SYSTEM's own profile (WindowsAppFolders as SYSTEM).
            var systemFolder = Path.Combine(Environment.SystemDirectory, "config", "systemprofile", "AppData", "Local", "ZoomIN");
            if (Directory.Exists(systemFolder))
            {
                Directory.Delete(systemFolder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            log.RemoveFailed(ex);
        }
    }
}
