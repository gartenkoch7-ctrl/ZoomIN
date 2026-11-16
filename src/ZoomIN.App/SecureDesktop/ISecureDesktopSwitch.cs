namespace ZoomIN.App.SecureDesktop;

/// <summary>
/// The "also on the sign-in screen" switch (ADR-0016). Reading works as the user; changing writes
/// HKLM, so it runs the installed ZoomIN.exe elevated with "--secure-desktop-setting on|off".
/// </summary>
public interface ISecureDesktopSwitch
{
    /// <summary>Only an installation in Program Files can be registered (not dev builds or the ZIP).</summary>
    bool IsAvailable { get; }

    bool IsEnabled { get; }

    /// <summary>Asks for administrator rights and switches. Blocks until done; call off the UI thread.</summary>
    /// <returns>True if the switch now has the requested state.</returns>
    bool RequestChange(bool on);

    /// <summary>The elevated step: registers or removes, and keeps the choice for updates.</summary>
    /// <returns>The process exit code: 0 on success.</returns>
    int ApplyElevated(bool on);
}
