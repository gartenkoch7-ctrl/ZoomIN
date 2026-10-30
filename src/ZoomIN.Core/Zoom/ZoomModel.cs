using ZoomIN.Core.Modes;

namespace ZoomIN.Core.Zoom;

/// <summary>
/// What the user wants: current mode, target zoom per monitor (1 = not zoomed) and last levels
/// per mode and monitor (M3/M4 decisions). Commands come from hotkey, mouse-hook and UI threads;
/// render threads read <see cref="Mode"/>, <see cref="TargetFor"/>, <see cref="Lens"/> and
/// <see cref="Dock"/> lock-free from immutable snapshots.
/// </summary>
public sealed class ZoomModel
{
    private static readonly IReadOnlyDictionary<string, double> s_allOff = new Dictionary<string, double>();

    private readonly Lock _gate = new();
    private readonly ZoomLevels _levels;
    private IReadOnlyDictionary<string, double> _targets = s_allOff;
    private LensSize _lens;
    private DockLayout _dock;
    private string _dockedMonitor = string.Empty;
    private double _animationSeconds;
    private int _mode;
    private bool _perMonitor;

    public ZoomModel(ZoomLevels levels, ZoomMode mode, bool perMonitor, double animationSeconds)
    {
        _levels = levels;
        _mode = (int)mode;
        _perMonitor = perMonitor;
        _animationSeconds = Math.Max(0, animationSeconds);
        _lens = LensSize.Default;
        _dock = DockLayout.Default;
    }

    /// <summary>Raised after every change, on the thread that caused it. Handlers must be fast.</summary>
    public event Action? Changed;

    public ZoomMode Mode => (ZoomMode)Volatile.Read(ref _mode);

    public bool IsOn => Volatile.Read(ref _targets).Values.Any(level => level > ZoomLimits.Min);

    /// <summary>Monitor showing the docked strip while <see cref="Mode"/> is Docked.</summary>
    public string DockedMonitor => Volatile.Read(ref _dockedMonitor);

    public LensSize Lens
    {
        get => Volatile.Read(ref _lens);
        set => Volatile.Write(ref _lens, value);
    }

    public DockLayout Dock
    {
        get => Volatile.Read(ref _dock);
        set => Volatile.Write(ref _dock, value.Normalized());
    }

    public double AnimationSeconds
    {
        get => Volatile.Read(ref _animationSeconds);
        set => Volatile.Write(ref _animationSeconds, Math.Max(0, value));
    }

    public bool PerMonitor
    {
        get { lock (_gate) { return _perMonitor; } }
        set { lock (_gate) { _perMonitor = value; } }
    }

    /// <summary>Target for one monitor; 1 when unknown or off. Called per frame, does not allocate.</summary>
    public double TargetFor(string monitorId) =>
        Volatile.Read(ref _targets).TryGetValue(monitorId, out var level) ? level : ZoomLimits.Min;

    public double LastLevelFor(ZoomMode mode, string monitorId)
    {
        lock (_gate)
        {
            return _levels.Get(mode, monitorId, _perMonitor);
        }
    }

    /// <summary>Runs <paramref name="read"/> under the lock, e.g. to copy levels for saving.</summary>
    public T Read<T>(Func<ZoomLevels, T> read)
    {
        lock (_gate)
        {
            return read(_levels);
        }
    }

    /// <summary>Strg+Alt+Umschalt+Pfeile: lens grows or shrinks in 5 % steps.</summary>
    public void ResizeLens(int widthSteps, int heightSteps)
    {
        Lens = Lens.Resize(widthSteps, heightSteps);
        Changed?.Invoke();
    }

    /// <summary>Strg+Alt+Z: anything zoomed → off; otherwise on in the current mode.</summary>
    public void Toggle(string monitorAtMouse, IReadOnlyCollection<string> monitors) =>
        Switch(Mode, monitorAtMouse, monitors, offIfOn: true);

    /// <summary>Strg+Alt+F/L/D: switch to the mode and zoom; the active mode's key switches off (M4 decision).</summary>
    public void Activate(ZoomMode mode, string monitorAtMouse, IReadOnlyCollection<string> monitors) =>
        Switch(mode, monitorAtMouse, monitors, offIfOn: mode == Mode);

    /// <summary>
    /// Zooms by wheel notches or key steps. Up from 1× starts zooming; down to 1× stops zooming.
    /// Applies to the monitor under the mouse, all monitors in shared mode, or the docked monitor.
    /// </summary>
    public void Step(double notches, string monitorAtMouse, IReadOnlyCollection<string> monitors)
    {
        lock (_gate)
        {
            var mode = Mode;
            var docked = mode == ZoomMode.Docked;
            var id = docked && AnyOn(monitors) ? DockedMonitor : monitorAtMouse;
            var current = TargetFor(id);
            var next = ZoomLimits.Step(current, notches);
            if (next == current)
            {
                return;
            }

            // Treat values within rounding distance of 1× as off.
            next = next < ZoomLimits.Min + 1e-6 ? ZoomLimits.Min : next;
            var targets = new Dictionary<string, double>(Volatile.Read(ref _targets));
            foreach (var monitor in docked || _perMonitor ? [id] : monitors)
            {
                targets[monitor] = next;
                if (next > ZoomLimits.Min)
                {
                    _levels.Set(mode, monitor, next, _perMonitor);
                }
            }

            if (docked)
            {
                Volatile.Write(ref _dockedMonitor, id);
            }

            Volatile.Write(ref _targets, targets);
        }

        Changed?.Invoke();
    }

    private void Switch(ZoomMode mode, string monitorAtMouse, IReadOnlyCollection<string> monitors, bool offIfOn)
    {
        lock (_gate)
        {
            if (offIfOn && AnyOn(monitors))
            {
                Volatile.Write(ref _targets, s_allOff);
            }
            else
            {
                Volatile.Write(ref _mode, (int)mode);
                Volatile.Write(ref _targets, TargetsFor(mode, monitorAtMouse, monitors));
            }
        }

        Changed?.Invoke();
    }

    private Dictionary<string, double> TargetsFor(ZoomMode mode, string monitorAtMouse, IReadOnlyCollection<string> monitors)
    {
        if (mode != ZoomMode.Docked)
        {
            return monitors.ToDictionary(id => id, id => _levels.Get(mode, id, _perMonitor));
        }

        // Docked: only the monitor under the mouse gets the strip (M4 decision).
        Volatile.Write(ref _dockedMonitor, monitorAtMouse);
        return new Dictionary<string, double> { [monitorAtMouse] = _levels.Get(mode, monitorAtMouse, _perMonitor) };
    }

    private bool AnyOn(IReadOnlyCollection<string> monitors) => monitors.Any(id => TargetFor(id) > ZoomLimits.Min);
}
