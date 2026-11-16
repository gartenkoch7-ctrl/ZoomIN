namespace ZoomIN.Core.Pointer;

/// <summary>
/// Mouse buffer (A6): how long the pointer has to press against a monitor edge before it may
/// cross to the neighbouring monitor. Global, not per profile; off from the factory.
/// </summary>
public enum EdgeBufferLevel
{
    Off,

    /// <summary>About 0.2 s.</summary>
    Light,

    /// <summary>About 0.4 s.</summary>
    Medium,

    /// <summary>About 0.8 s.</summary>
    Strong,
}
