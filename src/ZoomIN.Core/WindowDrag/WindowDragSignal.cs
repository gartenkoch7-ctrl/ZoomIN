namespace ZoomIN.Core.WindowDrag;

/// <summary>One step of a window drag, passed as a value (G1).</summary>
/// <param name="Stage">What happened.</param>
/// <param name="Kind">Move or resize for <see cref="WindowDragStage.Started"/>; unknown only for outline dragging with an unclear hit test.</param>
public readonly record struct WindowDragSignal(WindowDragStage Stage, WindowDragKind Kind = WindowDragKind.Unknown);
