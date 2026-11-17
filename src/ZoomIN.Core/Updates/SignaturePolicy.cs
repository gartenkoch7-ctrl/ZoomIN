namespace ZoomIN.Core.Updates;

/// <summary>
/// Which signature check results a release may have (ADR-0031): releases are unsigned and get
/// signed on the user's PC, a valid ZoomIN signature stays fine; a broken or foreign one is refused.
/// Authenticity itself comes from the published checksum, not from this check.
/// </summary>
public static class SignaturePolicy
{
    public static bool Accepts(PackageCheck check) => check is PackageCheck.Ok or PackageCheck.NotSigned;
}
