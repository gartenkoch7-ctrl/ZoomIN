namespace ZoomIN.Core.Updates;

/// <summary>
/// Signs the installed ZoomIN.exe on this PC (ADR-0031): releases are unsigned, but UIAccess needs a
/// signature Windows trusts. Runs only in the elevated install step.
/// </summary>
public interface ILocalSigner
{
    /// <summary>Signs <paramref name="executablePath"/> with a fresh certificate whose private key is deleted afterwards.</summary>
    /// <returns>False if signing failed; the new certificate is then trusted no longer and the install must stop.</returns>
    bool Sign(string executablePath);

    /// <summary>
    /// After the switch, or after a failure that followed signing: keeps trusting only the certificates that signed
    /// <paramref name="keptExecutables"/> (active and previous version). Best effort, never throws.
    /// </summary>
    void Prune(IEnumerable<string> keptExecutables);

    /// <summary>
    /// Removes every ZoomIN install certificate, and the key of any entry left in the personal store,
    /// from the machine (uninstall). A key whose store entry was never written (step killed in
    /// between) cannot be found; it is accessible to administrators only.
    /// </summary>
    void RemoveAll();
}
