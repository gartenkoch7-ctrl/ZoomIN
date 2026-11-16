using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.PointerFence;

/// <summary>
/// One step of the pointer fence's move chain (M19): mouse buffer, axis lock, island edit mode.
/// Called inside the low-level mouse hook for every pointer move, so it must only compute: no
/// allocation, no lock, no log, no I/O (Windows removes hooks that answer slowly). Registered
/// filters keep the hook installed; a filter that has nothing to do returns
/// <paramref name="requested"/> unchanged.
/// </summary>
public interface IPointerMoveFilter
{
    /// <summary>Position in the chain, lower first; see <see cref="PointerFilterOrder"/>.</summary>
    int Order { get; }

    /// <summary>Where the move may end; <paramref name="current"/> swallows it.</summary>
    /// <param name="timestamp">Of the move, in <see cref="TimeProvider.GetTimestamp"/> ticks.</param>
    /// <param name="current">Where the pointer is now.</param>
    /// <param name="requested">Where the move would put it, possibly already changed by earlier filters.</param>
    PhysicalPoint Filter(long timestamp, PhysicalPoint current, PhysicalPoint requested);
}
