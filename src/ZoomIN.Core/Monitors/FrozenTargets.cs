using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.Monitors;

/// <summary>
/// How zoom commands treat frozen monitors (G2), against one <see cref="FreezeState"/> snapshot: they
/// act on the free monitors only, a frozen monitor keeps its target for when it thaws, and "zoom is
/// on" counts the free monitors (commands) or all of them (what is saved for the next start).
/// </summary>
public static class FrozenTargets
{
    /// <summary>The monitors that are not frozen: zoom on/off and steps act on these only.</summary>
    public static IReadOnlyCollection<string> Free(FreezeState frozen, IReadOnlyCollection<string> monitors)
    {
        ArgumentNullException.ThrowIfNull(frozen);
        ArgumentNullException.ThrowIfNull(monitors);
        return frozen.Count == 0 ? monitors : [.. monitors.Where(id => !frozen.Contains(id))];
    }

    /// <summary><paramref name="next"/> with the frozen monitors' targets kept from <paramref name="current"/>.</summary>
    public static IReadOnlyDictionary<string, double> Keep(
        FreezeState frozen, IReadOnlyDictionary<string, double> next, IReadOnlyDictionary<string, double> current)
    {
        ArgumentNullException.ThrowIfNull(frozen);
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(current);
        if (frozen.Count == 0)
        {
            return next;
        }

        var merged = new Dictionary<string, double>(next, StringComparer.Ordinal);
        foreach (var monitor in frozen.Monitors)
        {
            if (current.TryGetValue(monitor.Id, out var level))
            {
                merged[monitor.Id] = level;
            }
            else
            {
                merged.Remove(monitor.Id);
            }
        }

        return merged;
    }

    /// <summary>Some free monitor is zoomed (what zoom on/off acts on).</summary>
    public static bool AnyFreeOn(FreezeState frozen, IReadOnlyDictionary<string, double> targets)
    {
        ArgumentNullException.ThrowIfNull(frozen);
        ArgumentNullException.ThrowIfNull(targets);
        foreach (var target in targets)
        {
            if (target.Value > ZoomLimits.Min && !frozen.Contains(target.Key))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Some frozen monitor shows a zoomed view: zoom counts as on for saving (secure desktop, next start).</summary>
    public static bool AnyFrozenZoomed(FreezeState frozen)
    {
        ArgumentNullException.ThrowIfNull(frozen);
        return frozen.Monitors.Any(monitor => monitor.Level > ZoomLimits.Min);
    }
}
