namespace ZoomIN.Core.Updates;

/// <summary>Which releases a user is offered (ADR-0014).</summary>
public enum ReleaseChannel
{
    /// <summary>Finished releases only, e.g. 1.0.0.</summary>
    Stable,

    /// <summary>Also release candidates and develop builds, e.g. 1.0.0-rc1 or 1.1.0-dev.42.</summary>
    Experimental,
}
