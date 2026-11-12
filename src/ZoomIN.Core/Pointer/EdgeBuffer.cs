using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Pointer;

/// <summary>
/// Mouse buffer (A6): the pointer stops at a monitor edge and crosses to the neighbouring monitor
/// only after pressing against the edge for the level's time. "Pressing" means the pointer stands
/// at the edge and further moves point outwards; moving back from the edge, or a pause longer than
/// <see cref="MaxPause"/>, starts over. A move filter of the pointer fence: it runs inside the
/// low-level mouse hook, only computes and never allocates.
/// </summary>
/// <remarks>
/// <see cref="Configure"/> may be called from any thread (it swaps an immutable setup); the
/// pressing state is only touched by <see cref="Filter"/>, i.e. by the hook thread. Frozen monitors
/// are not this filter's business: the fence applies forbidden areas after all filters.
/// </remarks>
public sealed class EdgeBuffer : IPointerMoveFilter
{
    /// <summary>Longer without an outward move and the pressing starts over.</summary>
    public static readonly TimeSpan MaxPause = TimeSpan.FromMilliseconds(250);

    private readonly TimeProvider _time;
    private Setup? _setup;

    // Hook thread only.
    private Setup? _pressSetup;
    private int _pressHome;
    private long _pressStart;
    private long _lastPress;
    private int _dirX;
    private int _dirY;

    public EdgeBuffer(TimeProvider time)
    {
        _time = time;
        PointerConstraintMetrics.EnsureCreated();
    }

    public int Order => PointerFilterOrder.MouseBuffer;

    /// <summary>True while the pointer presses against an edge (for tests and diagnostics).</summary>
    public bool IsPressing => _pressSetup is not null;

    /// <summary>Monitors in physical desktop pixels and the level; <see cref="EdgeBufferLevel.Off"/> lets every move through.</summary>
    public void Configure(IReadOnlyList<PhysicalRect> monitors, EdgeBufferLevel level)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        var press = PointerConstraintOptions.PressTime(level);
        Volatile.Write(ref _setup, press <= TimeSpan.Zero || monitors.Count < 2 ? null : new Setup(
            [.. monitors],
            Ticks(press),
            Ticks(MaxPause)));
    }

    public PhysicalPoint Filter(long timestamp, PhysicalPoint current, PhysicalPoint requested)
    {
        var setup = Volatile.Read(ref _setup);
        var from = setup is null ? -1 : IndexOf(setup.Monitors, current);
        if (from < 0)
        {
            // Off, a single monitor, or the pointer between monitors (display change): Windows decides.
            _pressSetup = null;
            return requested;
        }

        var home = setup!.Monitors[from];
        if (home.Contains(requested))
        {
            if (_pressSetup is not null && Retreats(home, requested))
            {
                _pressSetup = null;
            }

            return requested;
        }

        if (IndexOf(setup.Monitors, requested) < 0)
        {
            // Into no monitor: a diagonal past a corner (e.g. right and down along the bottom
            // edge) is a push along the one axis that leads onto a neighbour, as Windows would
            // clip it. Otherwise (a gap of an offset layout) it stays here, as at an outer edge,
            // and is no pressing: there is nothing to cross to.
            var alongX = new PhysicalPoint(requested.X, Clamp(home, requested).Y);
            var alongY = new PhysicalPoint(Clamp(home, requested).X, requested.Y);
            if (IndexOf(setup.Monitors, alongX) >= 0 && !home.Contains(alongX))
            {
                requested = alongX;
            }
            else if (IndexOf(setup.Monitors, alongY) >= 0 && !home.Contains(alongY))
            {
                requested = alongY;
            }
            else
            {
                return Clamp(home, requested);
            }
        }

        var dirX = requested.X >= home.Right ? 1 : requested.X < home.X ? -1 : 0;
        var dirY = requested.Y >= home.Bottom ? 1 : requested.Y < home.Y ? -1 : 0;
        if (!ContinuesPress(setup, from, timestamp, dirX, dirY))
        {
            _pressSetup = setup;
            _pressHome = from;
            _pressStart = timestamp;
            _dirX = dirX;
            _dirY = dirY;
            PointerConstraintMetrics.Blocked();
        }

        _lastPress = timestamp;
        if (timestamp - _pressStart >= setup.PressTicks)
        {
            _pressSetup = null;
            return requested;
        }

        return Clamp(home, requested);
    }

    private static PhysicalPoint Clamp(PhysicalRect home, PhysicalPoint point) => new(
        Math.Clamp(point.X, home.X, Math.Max(home.X, home.Right - 1)),
        Math.Clamp(point.Y, home.Y, Math.Max(home.Y, home.Bottom - 1)));

    // Same setup and home monitor, no long pause, and at least one outward direction in common
    // (at a corner the hand wobbles between "right" and "right and down").
    private bool ContinuesPress(Setup setup, int home, long timestamp, int dirX, int dirY) =>
        ReferenceEquals(_pressSetup, setup)
        && _pressHome == home
        && timestamp - _lastPress <= setup.MaxPauseTicks
        && ((dirX != 0 && dirX == _dirX) || (dirY != 0 && dirY == _dirY));

    // Moving away from every pressed edge; sliding along one of them keeps pressing (also from a
    // press that began diagonally at a corner).
    private bool Retreats(PhysicalRect home, PhysicalPoint requested)
    {
        var onX = (_dirX > 0 && requested.X >= home.Right - 1) || (_dirX < 0 && requested.X <= home.X);
        var onY = (_dirY > 0 && requested.Y >= home.Bottom - 1) || (_dirY < 0 && requested.Y <= home.Y);
        return !onX && !onY;
    }

    private static int IndexOf(PhysicalRect[] monitors, PhysicalPoint point)
    {
        for (var i = 0; i < monitors.Length; i++)
        {
            if (monitors[i].Contains(point))
            {
                return i;
            }
        }

        return -1;
    }

    private long Ticks(TimeSpan span) => (long)(span.TotalSeconds * _time.TimestampFrequency);

    private sealed record Setup(PhysicalRect[] Monitors, long PressTicks, long MaxPauseTicks);
}
