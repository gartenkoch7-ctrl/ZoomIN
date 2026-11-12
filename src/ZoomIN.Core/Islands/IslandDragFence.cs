using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Islands;

/// <summary>
/// Keeps the pointer on the monitor of an island drag (M19 B1): an island belongs to one monitor,
/// so dragging it, its corner or a rubber band stops at that monitor's edge instead of the pointer
/// slipping onto the next one. The first filter of the fence's chain (<see cref="PointerFilterOrder.IslandEdit"/>);
/// registered only while the edit mode runs, and it confines only while a drag runs.
/// <see cref="Filter"/> runs in the mouse hook: lock-free, no allocation.
/// </summary>
public sealed class IslandDragFence : IPointerMoveFilter
{
    private Bounds? _bounds;

    public int Order => PointerFilterOrder.IslandEdit;

    /// <summary>The monitor of the drag that starts, or null when it ends.</summary>
    public void Confine(PhysicalRect? monitor) =>
        Volatile.Write(ref _bounds, monitor is { } rect ? new Bounds(rect) : null);

    public PhysicalPoint Filter(long timestamp, PhysicalPoint current, PhysicalPoint requested)
    {
        if (Volatile.Read(ref _bounds) is not { } bounds)
        {
            return requested;
        }

        // Pixel grid as with RECT: the last reachable pixel is Right − 1.
        var rect = bounds.Rect;
        return new PhysicalPoint(
            Math.Clamp(requested.X, rect.X, Math.Max(rect.X, rect.Right - 1)),
            Math.Clamp(requested.Y, rect.Y, Math.Max(rect.Y, rect.Bottom - 1)));
    }

    // A reference, so a reader never sees half of a rectangle.
    private sealed record Bounds(PhysicalRect Rect);
}
