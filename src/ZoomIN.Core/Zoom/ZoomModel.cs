using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;

namespace ZoomIN.Core.Zoom;

/// <summary>
/// What the user wants: current mode, target zoom per monitor (1 = not zoomed) and last levels
/// per mode and monitor (M3/M4 decisions). Commands come from hotkey, mouse-hook and UI threads;
/// render threads read <see cref="Mode"/>, <see cref="EffectiveTargetFor"/>, <see cref="Lens"/> and
/// <see cref="Dock"/> lock-free from immutable snapshots. <see cref="Temporary"/> holds levels
/// that features apply for a while without changing what the user chose (M17).
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
    private readonly ParkedTargets _parked = new(); // under _gate: targets of frozen monitors (G2)

    public ZoomModel(ZoomLevels levels, ZoomMode mode, bool perMonitor, double animationSeconds)
    {
        _levels = levels;
        _mode = (int)mode;
        _perMonitor = perMonitor;
        _animationSeconds = Math.Max(0, animationSeconds);
        _lens = LensSize.Default;
        _dock = DockLayout.Default;
        Temporary = new TemporaryZoom(_gate, () => Mode == ZoomMode.Docked);
        Frozen.Changed += OnFrozenChanged;
    }

    /// <summary>Raised after every change, on the thread that caused it. Handlers must be fast.</summary>
    public event Action? Changed;

    /// <summary>Temporary levels per monitor (overview, window drag); never saved.</summary>
    public TemporaryZoom Temporary { get; }

    /// <summary>Frozen monitors (G2): own anchor, level and mode; their targets are parked meanwhile. Never saved.</summary>
    public MonitorFreeze Frozen { get; } = new();

    public ZoomMode Mode => (ZoomMode)Volatile.Read(ref _mode);

    public bool IsOn => Volatile.Read(ref _targets).Values.Any(level => level > ZoomLimits.Min);

    /// <summary>Monitor showing the docked strip while <see cref="Mode"/> is Docked.</summary>
    public string DockedMonitor => Volatile.Read(ref _dockedMonitor);

    // Setters below raise Changed so the settings window applies and saves changes live (M7).
    public LensSize Lens
    {
        get => Volatile.Read(ref _lens);
        set => Set(ref _lens, value.Resize(0, 0));
    }

    public DockLayout Dock
    {
        get => Volatile.Read(ref _dock);
        set => Set(ref _dock, value.Normalized());
    }

    public double AnimationSeconds
    {
        get => Volatile.Read(ref _animationSeconds);
        set
        {
            Volatile.Write(ref _animationSeconds, Math.Max(0, value));
            Changed?.Invoke();
        }
    }

    public bool PerMonitor
    {
        get { lock (_gate) { return _perMonitor; } }
        set
        {
            lock (_gate)
            {
                _perMonitor = value;
            }

            Changed?.Invoke();
        }
    }

    /// <summary>Selects the mode used by Strg+Alt+Z without switching zoom on.</summary>
    public void SetMode(ZoomMode mode)
    {
        IReadOnlyList<TemporaryZoomOwner> cleared;
        lock (_gate)
        {
            var changed = mode != Mode;
            Volatile.Write(ref _mode, (int)mode);
            cleared = changed ? Temporary.ClearLocked() : []; // a mode change ends temporary views
        }

        Raise(cleared);
    }

    /// <summary>Takes over all last levels, e.g. when switching profiles.</summary>
    public void ReplaceLevels(ZoomLevels levels)
    {
        lock (_gate)
        {
            _levels.CopyFrom(levels);
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// The user's target for one monitor; 1 when unknown or off. Ignores temporary levels, so
    /// settings, status texts and <see cref="IsOn"/> keep showing what the user chose.
    /// </summary>
    public double TargetFor(string monitorId) => ZoomTargetRules.TargetOf(Volatile.Read(ref _targets), monitorId);

    /// <summary>
    /// What the monitor shows: the user's target, lowered by a temporary level if one is active
    /// (never in Docked mode, see <see cref="TemporaryZoom"/>). Per frame: lock-free, 0 B.
    /// </summary>
    public double EffectiveTargetFor(string monitorId)
    {
        // Ceiling first: commands publish the new targets before they clear ceilings, so a cleared
        // ceiling read here guarantees the new target below (never the old target uncapped).
        var ceiling = Temporary.CeilingFor(monitorId);
        return Math.Min(TargetFor(monitorId), ceiling);
    }

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
    public void ResizeLens(int widthSteps, int heightSteps) => Lens = Lens.Resize(widthSteps, heightSteps);

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
        IReadOnlyList<TemporaryZoomOwner> cleared;
        lock (_gate)
        {
            var mode = Mode;
            var docked = mode == ZoomMode.Docked;
            var targets = Volatile.Read(ref _targets);
            monitors = FrozenTargets.Free(Frozen.State, monitors); // a frozen monitor keeps its level (G2)
            var id = docked && ZoomTargetRules.AnyOn(targets, monitors) ? DockedMonitor : monitorAtMouse;
            if (ZoomTargetRules.PlanStep(targets, id, notches, monitors, docked, _perMonitor) is not { } step)
            {
                return;
            }

            // Off is not a level to remember: the next switch-on restores the last one.
            if (step.Level > ZoomLimits.Min)
            {
                foreach (var monitor in step.Stepped)
                {
                    _levels.Set(mode, monitor, step.Level, _perMonitor);
                }
            }

            if (docked)
            {
                Volatile.Write(ref _dockedMonitor, id);
            }

            Volatile.Write(ref _targets, step.Targets);

            // Switching a monitor on or off is an explicit command: it ends temporary views there.
            // Cleared after the new targets, so a frame never sees the old target uncapped.
            cleared = step.Switched.Count > 0 ? Temporary.ClearLocked(step.Switched) : [];
        }

        Raise(cleared);
    }

    private void Switch(ZoomMode mode, string monitorAtMouse, IReadOnlyCollection<string> monitors, bool offIfOn)
    {
        IReadOnlyList<TemporaryZoomOwner> cleared;
        lock (_gate)
        {
            monitors = FrozenTargets.Free(Frozen.State, monitors); // a frozen monitor keeps its level (G2)
            if (offIfOn && ZoomTargetRules.AnyOn(Volatile.Read(ref _targets), monitors))
            {
                Volatile.Write(ref _targets, s_allOff);
            }
            else
            {
                // Docked: only the monitor under the mouse gets the strip (M4 decision).
                if (mode == ZoomMode.Docked)
                {
                    Volatile.Write(ref _dockedMonitor, monitorAtMouse);
                }

                Volatile.Write(ref _mode, (int)mode);
                Volatile.Write(ref _targets, ZoomTargetRules.SwitchOn(mode, monitorAtMouse, monitors, id => _levels.Get(mode, id, _perMonitor)));
            }

            // An explicit on/off or mode command ends every temporary view (see TemporaryZoom),
            // after the new targets so a frame never sees the old target uncapped.
            cleared = Temporary.ClearLocked();
        }

        Raise(cleared);
    }

    // A frozen monitor's target is parked, so commands and IsOn see only the free monitors; it comes
    // back on thaw (see ParkedTargets). IsOn may change: tray, saving and the docked reservation listen.
    private void OnFrozenChanged()
    {
        lock (_gate)
        {
            Volatile.Write(ref _targets, _parked.Update(Volatile.Read(ref _targets), Frozen.State, Mode, DockedMonitor));
        }

        Changed?.Invoke();
    }

    // Outside the lock: handlers may read the model.
    private void Raise(IReadOnlyList<TemporaryZoomOwner> temporaryCleared) => Temporary.AnnounceCommand(Changed, temporaryCleared);

    private void Set<T>(ref T field, T value)
        where T : class
    {
        Volatile.Write(ref field, value);
        Changed?.Invoke();
    }
}
