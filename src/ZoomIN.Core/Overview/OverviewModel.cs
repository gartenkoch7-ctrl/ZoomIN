using ZoomIN.Core.Modes;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.Overview;

/// <summary>
/// "Overview at the push of a key" (B2) as pure logic: tap the shortcut for an overview that stays,
/// tap again to zoom in where the frame is; hold it for an overview while held, release to zoom
/// in; Esc returns to the previous place. Thread-safe: presses come from the hotkey thread,
/// releases and Esc from the keyboard hook, cancels from zoom commands and pipeline rebuilds.
/// What an overview does (temporary 1×, frame, Esc, announcement) is up to the handlers of
/// <see cref="Started"/> and <see cref="Ended"/>; render threads read <see cref="Current"/> lock-free.
/// </summary>
public sealed class OverviewModel
{
    private readonly Lock _gate = new();
    private readonly TimeProvider _time;
    private OverviewSession? _current;
    private OverviewPhase _phase;
    private long _lastId;

    public OverviewModel(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
    }

    /// <summary>An overview started; raised outside the lock on the thread of the press.</summary>
    public event Action<OverviewSession>? Started;

    /// <summary>
    /// An overview ended; raised outside the lock. A handler may see it before the
    /// <see cref="Started"/> of the same overview finished on another thread.
    /// </summary>
    public event Action<OverviewEnded>? Ended;

    /// <summary>The running overview, or null. Lock-free (render threads read it every frame).</summary>
    public OverviewSession? Current => Volatile.Read(ref _current);

    public OverviewPhase Phase
    {
        get
        {
            lock (_gate)
            {
                return _phase;
            }
        }
    }

    /// <summary>Docked mode has no temporary levels and 1× has nothing to give an overview of.</summary>
    public static bool CanStart(ZoomMode mode, double userLevel) => mode != ZoomMode.Docked && userLevel > ZoomLimits.Min;

    /// <summary>
    /// The shortcut went down: starts an overview on the monitor, or zooms in at the frame if one
    /// is running. Does nothing if <see cref="CanStart"/> says no.
    /// </summary>
    /// <returns>The overview this press started and that still runs, or null.</returns>
    public OverviewSession? Press(string monitorId, ZoomMode mode, double userLevel)
    {
        ArgumentNullException.ThrowIfNull(monitorId);
        OverviewSession? started = null;
        OverviewEnded? ended = null;
        lock (_gate)
        {
            if (_current is not null)
            {
                // A second press: tapped before, or the release was missed (counts as tapped too).
                ended = EndLocked(OverviewEnd.ZoomIn, held: false);
            }
            else if (CanStart(mode, userLevel))
            {
                started = new OverviewSession(++_lastId, monitorId, mode, userLevel, _time.GetTimestamp());
                _phase = OverviewPhase.Pressed;
                Volatile.Write(ref _current, started);
            }
        }

        Raise(started, ended);

        // A Started handler may already have cancelled it (zoom command between press and set-up).
        return ReferenceEquals(Current, started) ? started : null;
    }

    /// <summary>
    /// The shortcut (main key or a modifier) was released: within <see cref="OverviewTiming.TapLimit"/>
    /// the overview stays (tapped), later it zooms in (held). Ignored unless the shortcut is down.
    /// </summary>
    public void Release() => Release(null, _time.GetTimestamp());

    /// <summary>
    /// The shortcut of overview <paramref name="sessionId"/> was released at <paramref name="releasedAt"/>
    /// (<see cref="TimeProvider.GetTimestamp"/> taken when the key went up, so a late delivery does
    /// not turn a tap into a hold; null = unknown, counts as a tap). Ignored if another overview runs.
    /// </summary>
    public void Release(long sessionId, long? releasedAt) => Release((long?)sessionId, releasedAt);

    /// <summary>Esc for overview <paramref name="sessionId"/>; false if it no longer runs.</summary>
    public bool Escape(long sessionId) => End(OverviewEnd.Escape, current => current.Id == sessionId);

    private void Release(long? sessionId, long? releasedAt)
    {
        OverviewEnded? ended = null;
        lock (_gate)
        {
            if (_phase != OverviewPhase.Pressed || _current is not { } session || (sessionId is { } id && session.Id != id))
            {
                return;
            }

            if (releasedAt is not { } at || OverviewTiming.IsTap(_time.GetElapsedTime(session.StartedAt, at)))
            {
                _phase = OverviewPhase.Tapped;
                return;
            }

            ended = EndLocked(OverviewEnd.ZoomIn, held: true);
        }

        Raise(null, ended);
    }

    /// <summary>Esc: ends the overview and returns to the previous place. False if none runs (Esc goes on to the app).</summary>
    public bool Escape() => End(OverviewEnd.Escape, _ => true);

    /// <summary>Ends this overview if it still runs, e.g. after a zoom command cleared its level.</summary>
    public bool Cancel(OverviewSession session) => End(OverviewEnd.Cancelled, current => ReferenceEquals(current, session));


    private bool End(OverviewEnd end, Func<OverviewSession, bool> applies)
    {
        OverviewEnded ended;
        lock (_gate)
        {
            if (_current is not { } session || !applies(session))
            {
                return false;
            }

            var held = _phase == OverviewPhase.Pressed && !OverviewTiming.IsTap(_time.GetElapsedTime(session.StartedAt));
            ended = EndLocked(end, held);
        }

        Raise(null, ended);
        return true;
    }

    private OverviewEnded EndLocked(OverviewEnd end, bool held)
    {
        var session = _current!;
        _phase = OverviewPhase.Off;
        Volatile.Write(ref _current, null);
        return new OverviewEnded(session, end, held);
    }

    private void Raise(OverviewSession? started, OverviewEnded? ended)
    {
        if (ended is { } e)
        {
            Ended?.Invoke(e);
        }

        if (started is not null)
        {
            Started?.Invoke(started);
        }
    }
}
