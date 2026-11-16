namespace ZoomIN.Core.Updates;

/// <summary>
/// Starts "&lt;exe&gt; --apply-update …" with the administrator prompt and waits for it (ADR-0015).
/// Used by the in-app updater and by the setup.
/// </summary>
public interface IElevatedStep
{
    /// <returns><see cref="UpdateResult.Declined"/> if the user said "No" at the prompt.</returns>
    UpdateResult Run(string executable, UpdateCommand command);

    /// <summary>SHA-256 of the package as lowercase hex, the form <see cref="UpdateCommand"/> carries.</summary>
    string HashOf(string path);
}
