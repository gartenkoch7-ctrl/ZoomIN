namespace ZoomIN.Core.Monitors;

/// <summary>Where <see cref="MonitorCycle"/> walks next along the monitor layout (G2).</summary>
public enum CycleDirection
{
    /// <summary>Towards the left (smaller centre x), or up in the same column.</summary>
    Left = -1,

    /// <summary>Towards the right (larger centre x), or down in the same column; the start direction.</summary>
    Right = 1,
}
