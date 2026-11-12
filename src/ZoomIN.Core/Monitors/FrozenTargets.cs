using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.Monitors;

/// <summary>
/// How zoom commands treat frozen monitors (G2): they act on the free monitors only (a frozen
/// monitor's target is parked meanwhile, see the zoom model), and what is saved as "zoom on"
/// counts a frozen zoomed monitor too.
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

    /// <summary>Some frozen monitor shows a zoomed view: zoom counts as on for saving (secure desktop, next start).</summary>
    public static bool AnyFrozenZoomed(FreezeState frozen)
    {
        ArgumentNullException.ThrowIfNull(frozen);
        return frozen.Monitors.Any(monitor => monitor.Level > ZoomLimits.Min);
    }
}
