namespace ZoomIN.Core.Zoom;

/// <summary>
/// What the user wants per monitor: a target zoom (1 = not zoomed) and the last level to toggle
/// back to. By default every monitor has its own level; <see cref="PerMonitor"/> = false shares
/// one level (M3 decision). Commands come from hotkey, mouse-hook and UI threads; the render
/// threads read <see cref="TargetFor"/> lock-free from an immutable snapshot.
/// </summary>
public sealed class ZoomModel
{
    private static readonly IReadOnlyDictionary<string, double> s_allOff = new Dictionary<string, double>();

    private readonly Lock _gate = new();
    private readonly Dictionary<string, double> _lastLevels;
    private IReadOnlyDictionary<string, double> _targets = s_allOff;
    private double _sharedLastLevel;
    private double _animationSeconds;
    private bool _perMonitor;

    public ZoomModel(
        double sharedLastLevel,
        IReadOnlyDictionary<string, double> monitorLevels,
        bool perMonitor,
        double animationSeconds)
    {
        _sharedLastLevel = ValidLevel(sharedLastLevel);
        _lastLevels = monitorLevels.ToDictionary(pair => pair.Key, pair => ValidLevel(pair.Value));
        _perMonitor = perMonitor;
        _animationSeconds = Math.Max(0, animationSeconds);
    }

    /// <summary>Raised after every change, on the thread that caused it. Handlers must be fast.</summary>
    public event Action? Changed;

    public bool IsOn => Volatile.Read(ref _targets).Values.Any(level => level > ZoomLimits.Min);

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

    public double SharedLastLevel
    {
        get { lock (_gate) { return _sharedLastLevel; } }
    }

    /// <summary>Target for one monitor; 1 when unknown or off. Called per frame, does not allocate.</summary>
    public double TargetFor(string monitorId) =>
        Volatile.Read(ref _targets).TryGetValue(monitorId, out var level) ? level : ZoomLimits.Min;

    public double LastLevelFor(string monitorId)
    {
        lock (_gate)
        {
            return LastLevel(monitorId);
        }
    }

    public IReadOnlyDictionary<string, double> LastLevels()
    {
        lock (_gate)
        {
            return new Dictionary<string, double>(_lastLevels);
        }
    }

    /// <summary>Any monitor zoomed → all off; otherwise every monitor to its last level. Glides.</summary>
    public void Toggle(IReadOnlyCollection<string> monitors)
    {
        lock (_gate)
        {
            var anyOn = monitors.Any(id => TargetFor(id) > ZoomLimits.Min);
            Publish(anyOn ? s_allOff : monitors.ToDictionary(id => id, LastLevel));
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Zooms the monitor under the mouse (or all, in shared mode) by wheel notches or key steps.
    /// Up from 1× starts zooming; down to 1× stops zooming that monitor (M2 decision).
    /// </summary>
    public void Step(double notches, string monitorId, IReadOnlyCollection<string> monitors)
    {
        lock (_gate)
        {
            var current = TargetFor(monitorId);
            var next = ZoomLimits.Step(current, notches);
            if (next == current)
            {
                return;
            }

            // Treat values within rounding distance of 1× as off.
            next = next < ZoomLimits.Min + 1e-6 ? ZoomLimits.Min : next;
            var targets = new Dictionary<string, double>(Volatile.Read(ref _targets));
            foreach (var id in _perMonitor ? [monitorId] : monitors)
            {
                targets[id] = next;
                if (next > ZoomLimits.Min)
                {
                    _lastLevels[id] = next;
                }
            }

            if (!_perMonitor && next > ZoomLimits.Min)
            {
                _sharedLastLevel = next;
            }

            Publish(targets);
        }

        Changed?.Invoke();
    }

    private double LastLevel(string monitorId) =>
        _perMonitor && _lastLevels.TryGetValue(monitorId, out var level) ? level : _sharedLastLevel;

    private void Publish(IReadOnlyDictionary<string, double> targets) => Volatile.Write(ref _targets, targets);

    private static double ValidLevel(double level) =>
        level > ZoomLimits.Min ? ZoomLimits.Clamp(level) : ZoomLimits.DefaultLevel;
}
