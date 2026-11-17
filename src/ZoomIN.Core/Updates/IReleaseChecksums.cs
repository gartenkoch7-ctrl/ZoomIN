namespace ZoomIN.Core.Updates;

/// <summary>
/// The published checksum of a release package, fetched by the elevated install step itself
/// (ADR-0031): the hash on its command line comes from a non-elevated caller and proves nothing,
/// so every package must match this one before it is installed (and, if unsigned, signed).
/// </summary>
public interface IReleaseChecksums
{
    /// <returns>SHA-256 of the package of <paramref name="version"/> from its GitHub release, or null if unavailable.</returns>
    byte[]? Published(AppVersion version);
}
