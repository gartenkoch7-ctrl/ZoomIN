using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Monitors;

/// <summary>Which freezes survive a display change (G2): the pointer must never end up trapped or invisible.</summary>
public static class FreezeRules
{
    /// <summary>
    /// The freezes to keep after a display change. A freeze ends where its monitor is gone or has other
    /// bounds, and for a still image (the rebuilt pipeline cannot keep it). All end if no free monitor
    /// is left, or if Windows put the pointer onto a frozen monitor (whose view neither draws nor follows it).
    /// </summary>
    /// <param name="monitors">The monitors now, from one enumeration.</param>
    /// <param name="mouse">Where the pointer is now; null if unknown.</param>
    public static IReadOnlyList<FrozenMonitor> KeptAfterDisplayChange(
        FreezeState state, IReadOnlyList<(string Id, PhysicalRect Bounds)> monitors, PhysicalPoint? mouse)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(monitors);
        var kept = state.Monitors
            .Where(frozen => !frozen.Still && monitors.Any(m => m.Id == frozen.Id && m.Bounds == frozen.Bounds))
            .ToList();
        var anyFree = monitors.Any(m => kept.TrueForAll(frozen => frozen.Id != m.Id));
        var pointerTrapped = mouse is { } p && kept.Exists(frozen => frozen.Bounds.Contains(p));
        return anyFree && !pointerTrapped ? kept : [];
    }
}
