namespace ZoomIN.Core.Tracking;

/// <summary>
/// Mouse, typing and hold key for the show conditions of the highlights (E1). Written by the hook
/// threads, read lock-free by the render threads (<see cref="Sample"/>, no allocation).
/// </summary>
public sealed class MarkerActivity : IHoldKeyHandler
{
    // Hold key pressed together with another key: Shift for a capital letter, not a hold.
    private const long Spoiled = -1;

    // The end of the running hold was already used to suppress a Ctrl tap.
    private const long Consumed = -1;

    // A Ctrl tap within this time after a real hold ended is the end of that hold, not "Ctrl alone".
    private static readonly TimeSpan TapAfterHold = TimeSpan.FromMilliseconds(500);

    private readonly TimeProvider _time;
    private long _lastActivity;
    private long _holdSince;
    private long _holdSeen;
    private long _holdEnded;
    private volatile MarkerHoldKey _key;

    /// <param name="time">
    /// Clock of the timestamps; the render threads compare them with <see cref="System.Diagnostics.Stopwatch"/>,
    /// so only <see cref="TimeProvider.System"/> or a test clock with the same tick rate fits.
    /// </param>
    public MarkerActivity(TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Shared setting (all profiles); the hook reads it for every key event.</summary>
    public MarkerHoldKey Key
    {
        get => _key;
        set
        {
            // The old key's release would no longer be recognised: forget a running hold.
            if (_key != value)
            {
                _key = value;
                Volatile.Write(ref _holdSince, 0);
            }
        }
    }

    /// <summary>
    /// For the render threads: one consistent enough read per frame. "Seen" is read before the start,
    /// the reverse of <see cref="HoldKeyDown"/>'s writes, so an old start never meets a fresh sign.
    /// </summary>
    public MarkerActivitySample Sample
    {
        get
        {
            var seen = Volatile.Read(ref _holdSeen);
            return new(Volatile.Read(ref _lastActivity), Volatile.Read(ref _holdSince), seen);
        }
    }

    /// <summary>The mouse moved (every move, hook thread).</summary>
    public void Moved() => Volatile.Write(ref _lastActivity, _time.GetTimestamp());

    /// <summary>A character key was typed.</summary>
    public void Typed() => Volatile.Write(ref _lastActivity, _time.GetTimestamp());

    /// <summary>
    /// With Ctrl as hold key, its release must not also count as "Ctrl alone" for the pointer
    /// locator. True once for a hold of at least <see cref="MarkerVisibility.MinHoldTime"/> that is
    /// still running (the other hook may see the release first) or ended just now; a later separate
    /// tap counts again.
    /// </summary>
    public bool ConsumeHoldEnd()
    {
        // A running hold counts only while the hook still confirms it (not a stale, missed one).
        var seen = Volatile.Read(ref _holdSeen);
        var since = Volatile.Read(ref _holdSince);
        if (since > 0 && _time.GetElapsedTime(since) >= MarkerVisibility.MinHoldTime
            && _time.GetElapsedTime(seen) < MarkerVisibility.HoldTimeout)
        {
            // Already used up: the coming release must not count again for a separate tap. If the
            // release came meanwhile (key state timer: the hold no longer runs), its end is used up
            // instead and cleared, so no Consumed is left behind for a later hold. Only if nothing
            // else wrote in between (compare-exchange).
            var endedBefore = Volatile.Read(ref _holdEnded);
            var released = Volatile.Read(ref _holdSince) == 0;
            Interlocked.CompareExchange(ref _holdEnded, released ? 0 : Consumed, endedBefore);
            return true;
        }

        var ended = Interlocked.Exchange(ref _holdEnded, 0);
        return ended > 0 && _time.GetElapsedTime(ended) < TapAfterHold;
    }

    // Key repeat (and the hook's key state check) sends "down" again while held: only the first one
    // starts the hold, every one proves the key is still down. Render threads poll every refresh.
    public void HoldKeyDown()
    {
        var now = _time.GetTimestamp();
        var seen = Volatile.Read(ref _holdSeen);

        // Nothing for longer than the timeout: the release was missed, this is a new press. The
        // start is written before "seen", so a render frame never pairs an old start with a fresh sign.
        if (seen != 0 && _time.GetElapsedTime(seen, now) >= MarkerVisibility.HoldTimeout)
        {
            Volatile.Write(ref _holdSince, now);
        }
        else
        {
            Interlocked.CompareExchange(ref _holdSince, now, 0);
        }

        Volatile.Write(ref _holdSeen, now);
    }

    public void HoldKeyUp()
    {
        var now = _time.GetTimestamp();
        var since = Interlocked.Exchange(ref _holdSince, 0);
        var held = since > 0 && _time.GetElapsedTime(since, now) >= MarkerVisibility.MinHoldTime;
        var consumed = Interlocked.Exchange(ref _holdEnded, 0) == Consumed;
        Volatile.Write(ref _holdEnded, held && !consumed ? now : 0);
    }

    public void OtherKeyDown()
    {
        if (Volatile.Read(ref _holdSince) != 0)
        {
            Volatile.Write(ref _holdSince, Spoiled);
        }
    }
}
