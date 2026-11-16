using ZoomIN.Core.Updates;

namespace ZoomIN.Core.SecureDesktop;

/// <summary>
/// The sign-in screen part of the elevated install, update and uninstall steps (ADR-0016). A failure
/// here never fails the installation itself: ZoomIN then only misses the secure desktops.
/// </summary>
public interface ISecureDesktopInstall
{
    /// <param name="choice">The setup's choice, or null for an update (keeps the user's preference, default on).</param>
    void Apply(InstallLayout layout, bool? choice);

    /// <summary>Removes the registration, the kept choice and the files of the SYSTEM instance.</summary>
    void Remove();
}
