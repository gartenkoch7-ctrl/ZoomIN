namespace ZoomIN.Core.Lifecycle;

/// <summary>What the user chose in the tray icon or its menu.</summary>
public enum TrayCommand
{
    /// <summary>Left click on the icon, or the "Settings" item.</summary>
    Open,
    ToggleZoom,
    Fullscreen,
    Lens,
    Docked,
    NextProfile,
    Exit,
}
