namespace ZoomIN.Core.Overview;

/// <summary>Where the overview stands (B2).</summary>
public enum OverviewPhase
{
    /// <summary>No overview.</summary>
    Off,

    /// <summary>The shortcut is down; its release decides between tapping and holding.</summary>
    Pressed,

    /// <summary>Tapped: the overview stays until the shortcut again (zoom in) or Esc.</summary>
    Tapped,
}
