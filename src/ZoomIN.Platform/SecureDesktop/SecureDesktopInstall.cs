using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Updates;

namespace ZoomIN.Platform.SecureDesktop;

/// <summary>
/// The sign-in screen part of the elevated install, update and uninstall steps (ADR-0016). A failure
/// here never fails the installation itself: ZoomIN then only misses the secure desktops.
/// </summary>
public static class SecureDesktopInstall
{
    /// <param name="choice">The setup's choice, or null for an update (keeps the user's preference, default on).</param>
    public static void Apply(InstallLayout layout, bool? choice, ILog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        try
        {
            var on = SecureDesktopRegistration.Default.Apply(layout, choice);
            log.Info($"[Install] Secure desktop support {(on ? "registered" : "off")}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            log.Error("[Install] Registering for the secure desktop failed", ex);
        }
    }

    /// <summary>Removes the registration, the kept choice and the files of the SYSTEM instance.</summary>
    public static void Remove(ILog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        try
        {
            SecureDesktopRegistration.Default.RemoveAll();
            var systemFolder = Path.Combine(Environment.SystemDirectory, "config", "systemprofile", "AppData", "Local", "ZoomIN");
            if (Directory.Exists(systemFolder))
            {
                Directory.Delete(systemFolder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            log.Error("[Uninstall] Removing the secure desktop support failed", ex);
        }
    }
}
