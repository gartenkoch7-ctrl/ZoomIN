namespace ZoomIN.Core.Updates;

/// <summary>Outcome of the elevated install step; the process exit code (ADR-0015).</summary>
public enum UpdateResult
{
    Installed = 0,
    InvalidArguments = 1,
    NotInstalled = 2,
    AlreadyActive = 3,
    PackageMismatch = 4,
    SignatureRejected = 5,
    Failed = 6,

    /// <summary>The user declined the administrator prompt; nothing changed.</summary>
    Declined = 7,
}
