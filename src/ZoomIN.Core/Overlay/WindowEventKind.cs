namespace ZoomIN.Core.Overlay;

/// <summary>Window events from other processes that may cover the overlay.</summary>
public enum WindowEventKind
{
    Other,
    Foreground,
    MenuPopupStart,
    Shown,
}
