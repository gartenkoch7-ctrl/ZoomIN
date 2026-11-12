using ZoomIN.Core.Modes;

namespace ZoomIN.Core.Zoom;

/// <summary>Pure rules for the targets that on/off and step commands produce (M3/M4 decisions).</summary>
internal static class ZoomTargetRules
{
    /// <summary>
    /// Plans a step on monitor <paramref name="id"/>: all monitors in shared mode, otherwise only
    /// that one. Null if the level does not change (already at 1× or 16×).
    /// </summary>
    public static ZoomStep? PlanStep(
        IReadOnlyDictionary<string, double> targets, string id, double notches, IReadOnlyCollection<string> monitors, bool docked, bool perMonitor)
    {
        var current = TargetOf(targets, id);
        var next = ZoomLimits.Step(current, notches);

        // Values within rounding distance of 1× count as off.
        next = next < ZoomLimits.Min + 1e-6 ? ZoomLimits.Min : next;
        if (next == current)
        {
            return null;
        }

        IReadOnlyCollection<string> stepped = docked || perMonitor ? [id] : monitors;
        var newTargets = new Dictionary<string, double>(targets);
        var switched = new List<string>();
        foreach (var monitor in stepped)
        {
            // A monitor going from off to on or back: an explicit command for that monitor.
            if ((TargetOf(targets, monitor) == ZoomLimits.Min) != (next == ZoomLimits.Min))
            {
                switched.Add(monitor);
            }

            newTargets[monitor] = next;
        }

        return new ZoomStep(newTargets, next, stepped, switched);
    }

    public static bool AnyOn(IReadOnlyDictionary<string, double> targets, IReadOnlyCollection<string> monitors) =>
        monitors.Any(id => TargetOf(targets, id) > ZoomLimits.Min);

    /// <summary>Targets when zoom switches on: each monitor's last level; Docked only the monitor under the mouse.</summary>
    public static Dictionary<string, double> SwitchOn(
        ZoomMode mode, string monitorAtMouse, IReadOnlyCollection<string> monitors, Func<string, double> lastLevel) =>
        mode == ZoomMode.Docked
            ? new Dictionary<string, double> { [monitorAtMouse] = lastLevel(monitorAtMouse) }
            : monitors.ToDictionary(id => id, lastLevel);

    /// <summary>
    /// Targets after a freeze changed (G2). A frozen monitor keeps its target through mode changes;
    /// once it thaws in Docked mode, only the docked monitor may be zoomed (one strip, one reservation),
    /// so a thawed other monitor goes back to 1×. Returns <paramref name="targets"/> if nothing changes.
    /// </summary>
    public static IReadOnlyDictionary<string, double> AfterFreezeChange(
        IReadOnlyDictionary<string, double> targets, ZoomMode mode, string dockedMonitor, Func<string, bool> isFrozen) =>
        mode == ZoomMode.Docked && targets.Keys.Any(id => id != dockedMonitor && !isFrozen(id))
            ? targets.Where(t => t.Key == dockedMonitor || isFrozen(t.Key)).ToDictionary(t => t.Key, t => t.Value)
            : targets;

    /// <summary>Target of one monitor; unknown monitors are off (1×). Per frame: 0 B.</summary>
    public static double TargetOf(IReadOnlyDictionary<string, double> targets, string id) =>
        targets.TryGetValue(id, out var level) ? level : ZoomLimits.Min;
}
