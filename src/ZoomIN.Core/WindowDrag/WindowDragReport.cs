namespace ZoomIN.Core.WindowDrag;

/// <summary>What one window drag did to the zoom, for its telemetry (G1). No window title.</summary>
/// <param name="Kind">Move or resize; unknown only for outline dragging with an unclear hit test.</param>
/// <param name="Action">The action chosen in the settings when the drag started.</param>
/// <param name="Monitor">Monitor under the pointer when the drag ended.</param>
/// <param name="LevelBefore">The user's level on the start monitor.</param>
/// <param name="Level">The temporary level, or null if it did not lower the zoom (left alone, or the user's level was lower already).</param>
/// <param name="CrossedMonitor">The pointer moved to another monitor during the drag.</param>
/// <param name="Cleared">A user command or a monitor set change ended the drag's effect early.</param>
public readonly record struct WindowDragReport(
    WindowDragKind Kind, WindowDragAction Action, string Monitor, double LevelBefore, double? Level, bool CrossedMonitor, bool Cleared);
