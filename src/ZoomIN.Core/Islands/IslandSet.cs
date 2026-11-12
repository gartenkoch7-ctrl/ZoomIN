using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Islands;

/// <summary>
/// The islands of the active profile (M19 B1): immutable, at most <see cref="IslandRules.MaxCount"/>,
/// drawn in list order (the last one on top). Every change returns a new set, which
/// <see cref="IslandModel"/> publishes with one reference swap; render threads read it lock-free.
/// </summary>
public sealed class IslandSet
{
    private readonly Island[] _items;

    private IslandSet(Island[] items, int nextId)
    {
        _items = items;
        NextId = nextId;
    }

    public static IslandSet Empty { get; } = new([], 1);

    /// <summary>Id the next added island gets; ids are never reused while ZoomIN runs.</summary>
    public int NextId { get; }

    public int Count => _items.Length;

    public bool IsFull => _items.Length >= IslandRules.MaxCount;

    public Island this[int index] => _items[index];

    public IReadOnlyList<Island> Items => _items;

    /// <summary>
    /// Islands from the settings: the first <see cref="IslandRules.MaxCount"/>, each fitted to its
    /// monitor if it is known now (an unknown monitor keeps the island for when it comes back).
    /// </summary>
    public static IslandSet From(IEnumerable<Island> islands, Func<string, PhysicalRect?>? boundsOf = null)
    {
        ArgumentNullException.ThrowIfNull(islands);
        var items = new List<Island>();
        foreach (var island in islands)
        {
            if (items.Count == IslandRules.MaxCount)
            {
                break;
            }

            if (island is null || string.IsNullOrEmpty(island.MonitorId))
            {
                continue;
            }

            var numbered = island with { Id = items.Count + 1 };
            items.Add(boundsOf?.Invoke(island.MonitorId) is { } monitor ? IslandRules.Fit(numbered, monitor) : numbered);
        }

        return new IslandSet([.. items], items.Count + 1);
    }

    /// <summary>
    /// Every island fitted to its monitor as it is now (the monitors may have changed since the
    /// islands were saved); islands of a missing monitor stay as they are. The same set if nothing moved.
    /// </summary>
    public IslandSet FitTo(Func<string, PhysicalRect?> boundsOf)
    {
        ArgumentNullException.ThrowIfNull(boundsOf);
        var items = (Island[])_items.Clone();
        var changed = false;
        for (var i = 0; i < items.Length; i++)
        {
            if (boundsOf(items[i].MonitorId) is { } monitor)
            {
                var fitted = IslandRules.Fit(items[i], monitor);
                changed |= fitted != items[i];
                items[i] = fitted;
            }
        }

        return changed ? new IslandSet(items, NextId) : this;
    }

    /// <summary>The same islands with ids from <paramref name="firstId"/> on (a profile's islands replace others).</summary>
    public IslandSet Renumbered(int firstId)
    {
        var items = new Island[_items.Length];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = _items[i] with { Id = firstId + i };
        }

        return new IslandSet(items, firstId + items.Length);
    }

    /// <summary>Adds an island (it gets <see cref="NextId"/>); null if the set is full.</summary>
    public IslandSet? Add(Island island)
    {
        ArgumentNullException.ThrowIfNull(island);
        return IsFull ? null : new IslandSet([.. _items, island with { Id = NextId }], NextId + 1);
    }

    public IslandSet Replace(int index, Island island)
    {
        ArgumentNullException.ThrowIfNull(island);
        var items = (Island[])_items.Clone();
        items[index] = island with { Id = _items[index].Id };
        return new IslandSet(items, NextId);
    }

    public IslandSet RemoveAt(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _items.Length);
        return new IslandSet([.. _items[..index], .. _items[(index + 1)..]], NextId);
    }

    /// <summary>Index of the island with <paramref name="id"/>, or −1.</summary>
    public int IndexOf(int id)
    {
        for (var i = 0; i < _items.Length; i++)
        {
            if (_items[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>True if an island lies on the monitor. Per frame: no allocation.</summary>
    public bool HasIslandOn(string monitorId)
    {
        for (var i = 0; i < _items.Length; i++)
        {
            if (string.Equals(_items[i].MonitorId, monitorId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
