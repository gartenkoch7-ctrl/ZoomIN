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
    /// Islands from the settings: the first <see cref="IslandRules.MaxCount"/> with a monitor, as
    /// saved. <see cref="IslandModel"/> fits them to the monitors (<see cref="FitTo"/>).
    /// </summary>
    public static IslandSet From(IEnumerable<Island> islands)
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

            items.Add(island with { Id = items.Count + 1 });
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
        Island[]? items = null; // copied on the first island that moves: usually nothing does
        for (var i = 0; i < _items.Length; i++)
        {
            if (boundsOf(_items[i].MonitorId) is { } monitor && IslandRules.Fit(_items[i], monitor) is var fitted && fitted != _items[i])
            {
                items ??= (Island[])_items.Clone();
                items[i] = fitted;
            }
        }

        return items is null ? this : new IslandSet(items, NextId);
    }

    /// <summary>
    /// These islands, but each one still as in <paramref name="start"/> (fitted to the monitors only
    /// for drawing) taken from <paramref name="saved"/> instead: what the edit mode commits, so islands
    /// the user did not touch keep their saved rectangles on a monitor that is smaller for a while.
    /// </summary>
    public IslandSet WithUntouchedFrom(IslandSet start, IslandSet saved)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(saved);
        var items = (Island[])_items.Clone();
        for (var i = 0; i < items.Length; i++)
        {
            var before = start.IndexOf(items[i].Id);
            var original = saved.IndexOf(items[i].Id);
            if (before >= 0 && original >= 0 && start[before] == items[i])
            {
                items[i] = saved[original];
            }
        }

        return new IslandSet(items, NextId);
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
