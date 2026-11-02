namespace ZoomIN.Core.Updates;

/// <summary>Outcome of downloading and verifying a release package (ADR-0014).</summary>
public enum PackageCheck
{
    Ok,
    DownloadFailed,

    /// <summary>The release lists no checksum for the package; it is never installed unchecked.</summary>
    ChecksumMissing,
    ChecksumMismatch,
    NotSigned,

    /// <summary>Windows does not trust the signature (unknown root, revoked, tampered file).</summary>
    SignatureInvalid,

    /// <summary>Validly signed, but not by ZoomIN – see <see cref="TrustedPublishers"/>.</summary>
    UnexpectedPublisher,
}
