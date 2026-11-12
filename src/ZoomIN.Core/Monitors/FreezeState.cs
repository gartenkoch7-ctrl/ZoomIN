using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Monitors;

/// <summary>
/// The set of frozen monitors (G2), immutable: a change is a new state, published by
/// <see cref="MonitorFreeze"/> with one atomic reference swap. Render threads look up their monitor
/// per frame with <see cref="For"/> (lock-free, 0 B).
/// </summary>
public sealed class FreezeState
{
    private readonly FrozenMonitor[] _monitors;

    private FreezeState(FrozenMonitor[] monitors)
    {
        _monitors = monitors;
    }

    public static FreezeState Empty { get; } = new([]);

    public IReadOnlyList<FrozenMonitor> Monitors => _monitors;

    public int Count => _monitors.Length;

    /// <summary>The freeze of this monitor, or null; per frame.</summary>
    public FrozenMonitor? For(string monitorId)
    {
        foreach (var monitor in _monitors)
        {
            if (string.Equals(monitor.Id, monitorId, StringComparison.Ordinal))
            {
                return monitor;
            }
        }

        return null;
    }

    public bool Contains(string monitorId) => For(monitorId) is not null;

    /// <summary>The bounds of all frozen monitors: the areas the pointer fence forbids.</summary>
    public IReadOnlyList<PhysicalRect> Areas() => [.. _monitors.Select(monitor => monitor.Bounds)];

    /// <summary>This state with <paramref name="monitor"/> added; an older freeze of the same monitor is replaced.</summary>
    internal FreezeState With(FrozenMonitor monitor) =>
        new([.. _monitors.Where(m => !string.Equals(m.Id, monitor.Id, StringComparison.Ordinal)), monitor]);

    /// <summary>This state without the monitors <paramref name="remove"/> selects.</summary>
    internal FreezeState Without(Func<FrozenMonitor, bool> remove, out IReadOnlyList<FrozenMonitor> removed)
    {
        removed = [.. _monitors.Where(remove)];
        return removed.Count == 0 ? this : _monitors.Length == removed.Count ? Empty : new([.. _monitors.Where(m => !remove(m))]);
    }
}
