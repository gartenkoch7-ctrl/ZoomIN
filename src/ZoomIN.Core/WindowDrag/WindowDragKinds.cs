using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.WindowDrag;

/// <summary>
/// Tells moving from resizing by the window's first change (G1 mini-spike, see
/// docs/kb/eingabe-und-tracking.md). Used when the hit test at the start was unclear; the hit
/// test itself is Win32 and lives in Platform.
/// </summary>
public static class WindowDragKinds
{
    /// <summary>
    /// Kind from the window's first change. Same size = moved. A snapped or maximized window that
    /// is moved gets its normal size back (<paramref name="normal"/>, the size Windows restores):
    /// moved as well. Otherwise a new size is a resize if an edge stayed where it was (the one
    /// opposite the dragged border); if all edges moved, it was moved.
    /// </summary>
    /// <param name="before">Window bounds at the start.</param>
    /// <param name="after">Window bounds after the first change.</param>
    /// <param name="normal">Bounds of the window when not snapped or maximized, if known; only its size counts.</param>
    public static WindowDragKind FromFirstChange(PhysicalRect before, PhysicalRect after, PhysicalRect? normal = null)
    {
        if (SameSize(before, after))
        {
            return WindowDragKind.Move;
        }

        if (normal is { } restored && !SameSize(before, restored) && SameSize(after, restored))
        {
            return WindowDragKind.Move;
        }

        var edgeKept = before.X == after.X || before.Y == after.Y || before.Right == after.Right || before.Bottom == after.Bottom;
        return edgeKept ? WindowDragKind.Resize : WindowDragKind.Move;
    }

    private static bool SameSize(PhysicalRect a, PhysicalRect b) => a.Width == b.Width && a.Height == b.Height;
}
