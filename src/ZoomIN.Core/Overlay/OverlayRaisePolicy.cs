namespace ZoomIN.Core.Overlay;

/// <summary>
/// Decides when the overlay must be put back on top of the topmost windows
/// (docs/kb/capture-ausschluss.md, "Z-Ebenen"). Context menus, browser popups and tooltips are
/// topmost windows created after the overlay, so they would sit above the zoom and appear twice:
/// magnified by Desktop Duplication and unmagnified on top.
/// </summary>
public static class OverlayRaisePolicy
{
    /// <param name="kind">The event that occurred.</param>
    /// <param name="isWholeWindow">The event concerns the window itself, not a child object (caret, cursor, list item).</param>
    /// <param name="isTopLevel">The window has no parent, so it competes with the overlay in z-order.</param>
    /// <param name="overlayVisible">Raising a hidden overlay is pointless.</param>
    public static bool ShouldRaise(WindowEventKind kind, bool isWholeWindow, bool isTopLevel, bool overlayVisible) =>
        overlayVisible && kind != WindowEventKind.Other && isWholeWindow && isTopLevel;
}
