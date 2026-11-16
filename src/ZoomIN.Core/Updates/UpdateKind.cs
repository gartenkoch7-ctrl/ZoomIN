namespace ZoomIN.Core.Updates;

/// <summary>What installing a release means relative to the running version.</summary>
public enum UpdateKind
{
    Upgrade,
    Reinstall,

    /// <summary>Settings and profiles are backed up first, an older version may not read them (ADR-0014).</summary>
    Downgrade,
}
