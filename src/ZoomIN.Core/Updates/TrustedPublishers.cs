namespace ZoomIN.Core.Updates;

/// <summary>
/// Signers whose executables may be installed (ADR-0014). A valid signature alone is not enough:
/// any trusted company could sign a file, so the signer itself is pinned.
/// </summary>
public static class TrustedPublishers
{
    /// <summary>The self-signed certificate of scripts/setup-signing.ps1; only trusted on the PC that created it.</summary>
    public const string LocalDevelopment = "CN=ZoomIN Local Code Signing";

    // The SignPath certificate subject is added once the OSS application is approved (M9).
    private static readonly string[] s_subjects = [LocalDevelopment];

    public static bool IsTrusted(string? subject) =>
        subject is not null && s_subjects.Contains(subject, StringComparer.OrdinalIgnoreCase);
}
