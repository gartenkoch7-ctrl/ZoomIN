using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Monitors;

/// <summary>
/// Last pointer position per monitor (G2): where Ctrl+Alt+N and the jump on freezing put the
/// pointer when it comes back to a monitor ("seine letzte Position, sonst die Mitte"). A position
/// is taken when a command moves the pointer away. Not thread-safe: its owner serialises access.
/// </summary>
public sealed class PointerMemory
{
    private readonly Dictionary<string, PhysicalPoint> _last = new(StringComparer.Ordinal);

    public void Remember(string monitorId, PhysicalPoint position)
    {
        ArgumentNullException.ThrowIfNull(monitorId);
        _last[monitorId] = position;
    }

    /// <summary>
    /// The remembered position if it still lies on the monitor (its bounds may have changed),
    /// otherwise the monitor's centre pixel.
    /// </summary>
    public PhysicalPoint Recall(string monitorId, PhysicalRect bounds)
    {
        ArgumentNullException.ThrowIfNull(monitorId);
        return _last.TryGetValue(monitorId, out var position) && bounds.Contains(position)
            ? position
            : new PhysicalPoint(bounds.X + Math.Floor(bounds.Width / 2), bounds.Y + Math.Floor(bounds.Height / 2));
    }

    /// <summary>Drops monitors that are gone, so a reused device name starts in its middle.</summary>
    public void KeepOnly(IReadOnlyCollection<string> monitorIds)
    {
        ArgumentNullException.ThrowIfNull(monitorIds);
        foreach (var id in _last.Keys.Where(id => !monitorIds.Contains(id)).ToList())
        {
            _last.Remove(id);
        }
    }
}
