using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;

namespace ZoomIN.Core.Zoom;

/// <summary>
/// Targets of frozen monitors, kept out of the model's targets while frozen (G2): a frozen monitor
/// shows its own level (<see cref="FrozenMonitor.Level"/>), so zoom on/off, steps and
/// <see cref="ZoomModel.IsOn"/> only see the free monitors. On thaw the level comes back if it still
/// fits: same mode as at freezing, in Docked mode only for the docked monitor (one strip, one
/// reservation), with a shared level the level the others have now; after a display change, and
/// otherwise, the monitor is at 1×. Works under the model's lock.
/// </summary>
internal sealed class ParkedTargets
{
    private readonly Dictionary<string, ZoomMode> _parked = new(StringComparer.Ordinal);
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

        // A freeze is published first (render threads then use its own level), then its target parks.
        // A thaw restores the target first, under the freeze lock, then publishes: never a dip to 1×.
        // Lock order freeze → model; the model never takes the freeze lock.
        frozen.Changed += OnChanged;
        frozen.Thawing = OnThawing;
    }

    private void OnChanged()
    {
        lock (_gate)
        {
            Dictionary<string, double>? next = null;
            var targets = _read();
            var mode = _context().Mode;
            foreach (var monitor in _frozen.State.Monitors)
            {
                if (_parked.TryAdd(monitor.Id, mode) && targets.ContainsKey(monitor.Id))
                {
                    next ??= new Dictionary<string, double>(targets);
                    next.Remove(monitor.Id);
                }
            }

            if (next is not null)
            {
                _write(next);
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
                if (!_parked.Remove(monitor.Id, out var parkedMode) || !restoreLevel || parkedMode != mode
                    || (mode == ZoomMode.Docked && monitor.Id != docked))
                {
                    continue;
                }

                // Shared level: the level the free monitors have now, if any is zoomed; else the frozen one.
                var shared = perMonitor ? 0 : targets.Values.Where(level => level > ZoomLimits.Min).DefaultIfEmpty(0).Max();
                var level = shared > 0 ? shared : monitor.Level;
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
