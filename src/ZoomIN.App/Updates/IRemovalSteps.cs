using ZoomIN.Core.Updates;

namespace ZoomIN.App.Updates;

/// <summary>
/// The process work of an uninstall (ADR-0014): the administrator step and what has to wait until
/// ZoomIN has ended. Behind an interface so <see cref="UninstallFlow"/> can be tested without UAC
/// prompts or deleting Program Files (M14 step 8).
/// </summary>
internal interface IRemovalSteps
{
    /// <summary>Runs "--remove-installation" elevated and waits for it.</summary>
    /// <returns>False if the user declined the administrator prompt or the step failed.</returns>
    bool RunElevated();

    /// <summary>Ends every other ZoomIN process except <paramref name="waitForProcessId"/>.</summary>
    void EndOtherInstances(int waitForProcessId);

    /// <summary>Deletes the installation folder once this process and <paramref name="waitForProcessId"/> have ended.</summary>
    void ScheduleFolderRemoval(InstallLayout layout, int waitForProcessId);
}
