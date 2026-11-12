namespace ZoomIN.Core.Tracking;

/// <summary>
/// Fades one highlight in or out in about 100 ms when its condition flips (E1 decision), so nothing
/// flashes. A mutable struct kept in a field of the render thread; no allocation.
/// </summary>
public struct MarkerFade
{
    /// <summary>Duration of a full fade.</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(100);

    private long _last;
    private bool _started;

    /// <summary>0 = hidden, 1 = fully shown.</summary>
    public double Alpha { get; private set; }

    /// <summary>Moves <see cref="Alpha"/> towards the target; the first call jumps (nothing to fade from).</summary>
    /// <returns>True if <see cref="Alpha"/> changed: the frame must be drawn again.</returns>
    public bool Update(bool visible, long now, long frequency)
    {
        var target = visible ? 1.0 : 0.0;
        var before = Alpha;
        if (!_started)
        {
            _started = true;
            Alpha = target;
        }
        else
        {
            var step = Math.Max(0, now - _last) / (Duration.TotalSeconds * frequency);
            Alpha = target > Alpha ? Math.Min(target, Alpha + step) : Math.Max(target, Alpha - step);
        }

        _last = now;
        return Alpha != before;
    }
}
