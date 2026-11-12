using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Monitors;

/// <summary>Which freezes end when the fence recomputes on its own (G2): the pointer must never end up trapped or invisible.</summary>
public static class FreezeRules
{
    /// <summary>
    /// The freezes a fence event ends. Back from another desktop (Win+L, UAC, Ctrl+Alt+Del) all of
    /// them (M10 decision); after a display change see <see cref="KeptAfterDisplayChange"/>. Only
    /// freezes that existed when the event came (or were being made then) count: a queued event must
    /// not end a freeze the user makes after it.
    /// </summary>
    /// <param name="now">The freezes now.</param>
    /// <param name="seen">The freezes when the event came.</param>
    /// <param name="freezing">A freeze was being made when the event came (moved, not yet published).</param>
    /// <param name="monitors">After a display change: the monitors now, from one enumeration; empty = undecided.</param>
    /// <param name="mouse">Where the pointer is now; null if unknown.</param>
    public static IReadOnlyList<FrozenMonitor> ToLift(
        FenceTrigger trigger, FreezeState now, FreezeState seen, bool freezing,
        IReadOnlyList<(string Id, PhysicalRect Bounds)> monitors, PhysicalPoint? mouse)
    {
        ArgumentNullException.ThrowIfNull(now);
        ArgumentNullException.ThrowIfNull(seen);
        ArgumentNullException.ThrowIfNull(monitors);
        var candidates = now.Monitors.Where(m => freezing || seen.Monitors.Contains(m)).ToList();
        switch (trigger)
        {
            case FenceTrigger.DesktopSwitch or FenceTrigger.SessionUnlock:
                return candidates;
            case FenceTrigger.DisplayChange when monitors.Count > 0:
                var kept = KeptAfterDisplayChange(now, monitors, mouse);
                return [.. candidates.Where(m => !kept.Contains(m))];
            default:
                return [];
        }
    }

    /// <summary>
    /// The freezes to keep after a display change. A freeze ends where its monitor is gone or has other
    /// bounds, and for a still image (the rebuilt pipeline cannot keep it). All end if no free monitor
    /// with an area is left, or if Windows put the pointer onto a frozen monitor (whose view neither
    /// draws nor follows it).
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

        // A monitor Windows reports without an area (asleep, being detached) is no place for the pointer.
        var anyFree = monitors.Any(m => m.Bounds is { Width: > 0, Height: > 0 } && kept.TrueForAll(frozen => frozen.Id != m.Id));
        var pointerTrapped = mouse is { } p && kept.Exists(frozen => frozen.Bounds.Contains(p));
        return anyFree && !pointerTrapped ? kept : [];
    }
}
