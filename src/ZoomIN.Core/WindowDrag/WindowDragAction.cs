namespace ZoomIN.Core.WindowDrag;

/// <summary>What happens to the zoom while a window is dragged (G1 decision: lift by default).</summary>
public enum WindowDragAction
{
    /// <summary>The monitor glides to 1×, filters stay.</summary>
    Lift,

    /// <summary>The monitor glides down to <see cref="WindowDragOptions.LowerLevel"/>, only if it is zoomed in further.</summary>
    Lower,

    /// <summary>The zoom stays as it is.</summary>
    Unchanged,
}
