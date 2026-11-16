using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.PointerFence;

/// <summary>
/// Moves the pointer (SetCursorPos). ADR-0028: only as the direct result of a shortcut the user
/// pressed (freeze, next monitor), never automatically; an architecture test keeps the callers
/// to the App's controllers. Each move is logged with its reason and target monitor.
/// </summary>
public interface IPointerMover
{
    /// <returns>False if Windows refused (e.g. another desktop is active).</returns>
    bool MoveTo(PhysicalPoint target, PointerMoveReason reason);
}
