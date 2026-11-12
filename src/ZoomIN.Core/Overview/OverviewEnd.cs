namespace ZoomIN.Core.Overview;

/// <summary>How an overview ended (B2); also the telemetry tag <c>zoomin.overview.end</c>.</summary>
public enum OverviewEnd
{
    /// <summary>Shortcut tapped again or released after holding: zoom in where the frame is.</summary>
    ZoomIn,

    /// <summary>Esc: back to the previous place and level.</summary>
    Escape,

    /// <summary>Ended by something else: a zoom command of the user, mode or display change.</summary>
    Cancelled,
}
