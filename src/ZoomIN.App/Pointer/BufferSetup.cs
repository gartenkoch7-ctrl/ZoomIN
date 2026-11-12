using ZoomIN.Core.Monitors;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;

namespace ZoomIN.App.Pointer;

/// <summary>
/// Feeds the mouse buffer (A6) its level and the current monitors, from settings changes and from
/// the fence's display-change notice. Its own small lock, never held around a fence call: the
/// fence's watcher thread refreshes here while the controller may hold its lock and wait for that
/// thread.
/// </summary>
internal sealed class BufferSetup(EdgeBuffer buffer, IMonitorProvider monitors)
{
    private readonly Lock _gate = new();
    private EdgeBufferLevel _level;
    private IReadOnlyList<PhysicalRect> _lastBounds = [];

    /// <summary>Sets the level, or with null keeps it, and reads the monitors; returns how many are known.</summary>
    public int Configure(EdgeBufferLevel? level)
    {
        lock (_gate)
        {
            // Read also while off: switching on later must not start from an old layout.
            _level = level ?? _level;
            if (monitors.AllBounds() is { Count: > 0 } bounds)
            {
                _lastBounds = bounds;
            }

            // Empty while Windows is still enumerating after a display change: keep the last known
            // monitors rather than switching the buffer off until the next change.
            buffer.Configure(_lastBounds, _level);
            return _lastBounds.Count;
        }
    }
}
