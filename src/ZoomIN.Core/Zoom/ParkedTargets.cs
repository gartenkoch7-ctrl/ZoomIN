using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;

namespace ZoomIN.Core.Zoom;

/// <summary>
/// Targets of frozen monitors, kept out of the model's targets while frozen (G2): a frozen monitor
/// shows its own level, so zoom on/off, steps and <see cref="ZoomModel.IsOn"/> only see the free
/// monitors. On thaw the target comes back if it still fits: same mode as at freezing, and in
/// Docked mode only for the docked monitor (one strip, one reservation); otherwise the monitor is at 1×.
/// Used under the model's lock.
/// </summary>
internal sealed class ParkedTargets
{
    private readonly Dictionary<string, (double Target, ZoomMode Mode)> _parked = new(StringComparer.Ordinal);

    /// <summary>The targets after a freeze change; <paramref name="targets"/> itself if nothing moves.</summary>
    public IReadOnlyDictionary<string, double> Update(
        IReadOnlyDictionary<string, double> targets, FreezeState frozen, ZoomMode mode, string dockedMonitor)
    {
        Dictionary<string, double>? next = null;
        foreach (var monitor in frozen.Monitors)
        {
            if (_parked.TryAdd(monitor.Id, (ZoomTargetRules.TargetOf(targets, monitor.Id), mode)) && targets.ContainsKey(monitor.Id))
            {
                next ??= new Dictionary<string, double>(targets);
                next.Remove(monitor.Id);
            }
        }

        foreach (var id in _parked.Keys.Where(id => !frozen.Contains(id)).ToList())
        {
            var (target, parkedMode) = _parked[id];
            _parked.Remove(id);
            if (target > ZoomLimits.Min && parkedMode == mode && (mode != ZoomMode.Docked || id == dockedMonitor))
            {
                next ??= new Dictionary<string, double>(targets);
                next[id] = target;
            }
        }

        return next ?? targets;
    }
}
