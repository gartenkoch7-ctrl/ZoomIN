namespace ZoomIN.Core.Zoom;

/// <summary>
/// What the user wants: target zoom (1 = off) and the last level for toggling back on.
/// Commands arrive from the hotkey, mouse-hook and UI threads; the render thread only reads
/// <see cref="Target"/> and <see cref="AnimationSeconds"/> (lock-free).
/// </summary>
public sealed class ZoomModel
{
    private readonly Lock _gate = new();
    private double _target = ZoomLimits.Min;
    private double _lastLevel;
    private double _animationSeconds;

    public ZoomModel(double lastLevel, double animationSeconds)
    {
        _lastLevel = lastLevel > ZoomLimits.Min ? ZoomLimits.Clamp(lastLevel) : ZoomLimits.DefaultLevel;
        _animationSeconds = Math.Max(0, animationSeconds);
    }

    /// <summary>Raised after every change, on the thread that caused it. Handlers must be fast.</summary>
    public event Action? Changed;

    public double Target => Volatile.Read(ref _target);

    public bool IsOn => Target > ZoomLimits.Min;

    /// <summary>Level Ctrl+Alt+Z returns to; always greater than 1×.</summary>
    public double LastLevel
    {
        get
        {
            lock (_gate)
            {
                return _lastLevel;
            }
        }
    }

    public double AnimationSeconds
    {
        get => Volatile.Read(ref _animationSeconds);
        set => Volatile.Write(ref _animationSeconds, Math.Max(0, value));
    }

    /// <summary>Off → last level, on → 1× (off). Both glide (M2 decision).</summary>
    public void Toggle()
    {
        lock (_gate)
        {
            Volatile.Write(ref _target, _target > ZoomLimits.Min ? ZoomLimits.Min : _lastLevel);
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Zooms by wheel notches or key steps. Stepping up from off starts at 1× and grows;
    /// stepping down to 1× switches zoom off (M2 decision).
    /// </summary>
    public void Step(double notches)
    {
        lock (_gate)
        {
            var next = ZoomLimits.Step(_target, notches);
            if (next == _target)
            {
                return;
            }

            // Treat values within rounding distance of 1× as off.
            if (next < ZoomLimits.Min + 1e-6)
            {
                next = ZoomLimits.Min;
            }
            else
            {
                _lastLevel = next;
            }

            Volatile.Write(ref _target, next);
        }

        Changed?.Invoke();
    }
}
