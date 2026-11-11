namespace ZoomIN.Core.WindowDrag;

/// <summary>What the user does with the window (G1). Windows only says "move or size started".</summary>
public enum WindowDragKind
{
    /// <summary>
    /// The hit test was unclear; the source then decides by the window's first change. Stays
    /// unknown only with outline dragging, where the window does not change until the release.
    /// </summary>
    Unknown,

    /// <summary>The window is moved (title bar, Alt+Space → Move, or the app's own drag area).</summary>
    Move,

    /// <summary>The window is resized at its border or corner.</summary>
    Resize,
}
