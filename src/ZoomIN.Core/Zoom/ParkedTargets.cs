using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;

namespace ZoomIN.Core.Zoom;

/// <summary>
/// Keeps frozen monitors out of the model's targets (G2): a frozen monitor shows its own level
/// (<see cref="FrozenMonitor.Level"/>), so zoom on/off, steps and <see cref="ZoomModel.IsOn"/> only
/// see the free monitors. On thaw the level comes back if it still fits: same mode as at freezing
/// (<see cref="FrozenMonitor.Mode"/>), in Docked mode only for the docked monitor (one strip, one
/// reservation), with a shared level only the level the others have now (none zoomed: 1×); after a
/// display change and otherwise the monitor is at 1×. Stateless; works under the model's lock.
/// </summary>
internal sealed class ParkedTargets
{
    private readonly Lock _gate;
    private readonly MonitorFreeze _frozen;
    private readonly Func<IReadOnlyDictionary<string, double>> _read;
    private readonly Action<IReadOnlyDictionary<string, double>> _write;
    private readonly Func<(ZoomMode Mode, string Docked, bool PerMonitor)> _context;
    private readonly Action _changed;

    /// <param name="gate">The model's lock.</param>
    /// <param name="changed">Raises the model's Changed: IsOn may change (tray, saving, docked reservation listen).</param>
    public ParkedTargets(
        Lock gate, MonitorFreeze frozen, Func<IReadOnlyDictionary<string, double>> read, Action<IReadOnlyDictionary<string, double>> write,
        Func<(ZoomMode Mode, string Docked, bool PerMonitor)> context, Action changed)
    {
        (_gate, _frozen, _read, _write, _context, _changed) = (gate, frozen, read, write, context, changed);

        // A freeze is published first (render threads then use its own level), then its target leaves.
        // A thaw restores the target first, under the freeze lock, then publishes: never a dip to 1×.
        // Lock order freeze → model; the model never takes the freeze lock.
        frozen.Changed += OnChanged;
        frozen.Thawing = OnThawing;
    }

    private void OnChanged()
    {
        lock (_gate)
        {
            var targets = _read();
            var frozen = _frozen.State;
            if (targets.Keys.Any(frozen.Contains))
            {
                _write(targets.Where(t => !frozen.Contains(t.Key)).ToDictionary(t => t.Key, t => t.Value));
            }
        }

        _changed();
    }

    private void OnThawing(IReadOnlyList<FrozenMonitor> thawed, bool restoreLevel)
    {
        lock (_gate)
        {
            var targets = _read();
            var (mode, docked, perMonitor) = _context();
            Dictionary<string, double>? next = null;
            foreach (var monitor in thawed)
            {
                if (!restoreLevel || monitor.Mode != mode || (mode == ZoomMode.Docked && monitor.Id != docked))
                {
                    continue;
                }

                // Shared level: the level the free monitors have now; none zoomed means zoom is off.
                var level = perMonitor ? monitor.Level : targets.Values.DefaultIfEmpty(ZoomLimits.Min).Max();
                if (level > ZoomLimits.Min)
                {
                    next ??= new Dictionary<string, double>(targets);
                    next[monitor.Id] = level;
                }
            }

            if (next is not null)
            {
                _write(next);
            }
        }
    }
}
