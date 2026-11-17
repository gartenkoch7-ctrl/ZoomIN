namespace ZoomIN.Core.Updates;

/// <summary>
/// Signers whose executables may be installed (ADR-0014). A valid signature alone is not enough:
/// any trusted company could sign a file, so the signer itself is pinned.
/// </summary>
public static class TrustedPublishers
{
    /// <summary>The self-signed certificate of scripts/setup-signing.ps1; only trusted on the PC that created it.</summary>
    public const string LocalDevelopment = "CN=ZoomIN Local Code Signing";

    /// <summary>The certificate the install step creates on each PC (ADR-0031); trusted only there.</summary>
    public const string LocalInstall = "CN=ZoomIN Local Signing";

    // A public certificate (SignPath or similar) would be added here; releases are unsigned until then (ADR-0031).
    private static readonly string[] s_subjects = [LocalDevelopment, LocalInstall];

    public static bool IsTrusted(string? subject) =>
        subject is not null && s_subjects.Contains(subject, StringComparer.OrdinalIgnoreCase);
}
