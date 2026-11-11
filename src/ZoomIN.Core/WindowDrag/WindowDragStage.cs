namespace ZoomIN.Core.WindowDrag;

/// <summary>Steps of a window drag (G1).</summary>
public enum WindowDragStage
{
    /// <summary>
    /// The window changed for the first time in a move or size loop (outline dragging: the pointer
    /// moved past the drag threshold). Not at the button press: a plain click on a title bar
    /// enters the same loop and must not touch the zoom.
    /// </summary>
    Started,

    /// <summary>The pointer moved onto another monitor during the drag.</summary>
    MonitorChanged,

    /// <summary>The drag ended (released, cancelled, or the window is gone).</summary>
    Ended,
}
