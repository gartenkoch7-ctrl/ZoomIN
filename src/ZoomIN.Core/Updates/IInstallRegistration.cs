namespace ZoomIN.Core.Updates;

/// <summary>
/// Machine-wide traces of an installation (ADR-0014): the entry in Windows "Apps" and the Start
/// menu shortcut for all users. Needs administrator rights, so it runs in the elevated step.
/// </summary>
public interface IInstallRegistration
{
    void Register(InstallLayout layout, AppVersion version);

    void Unregister();

    /// <summary>Silently removes the old per-user Inno Setup installation (M8), if there is one.</summary>
    /// <returns>True if one was found and its uninstaller ran.</returns>
    bool RemoveLegacyInstall(TimeSpan timeout);
}
