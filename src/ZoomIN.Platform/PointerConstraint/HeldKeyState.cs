namespace ZoomIN.Platform.PointerConstraint;

/// <summary>
/// Whether the axis lock's key is held (A6), fed by the keyboard hook and the release check.
/// One small lock serializes press, release and check, so a press and a concurrent stop can never
/// leave the axis lock held without the key. Nothing slow happens under it: the hook waits on it.
/// The check timer only runs while the key counts as held (plus one last tick after a release
/// that reports failed notifications).
/// </summary>
internal sealed class HeldKeyState(Func<int, bool> isDown, TimeSpan checkInterval)
{
    // Checks (0.5 s apart) that may read "up" after a fake key up before the key counts as released.
    private const int FakeUpChecks = 4;

    private readonly Lock _state = new();
    private Watch? _watch;
    private ITimer? _timer;
    private bool _held;
    private bool _fakeUp;
    private int _fakeUpChecks;
    private bool _fakeReleased;
    private int _failures;

    public bool IsWatching
    {
        get
        {
            lock (_state)
            {
                return _watch is not null;
            }
        }
    }

    /// <summary>Watches a new key; a key held under the old watch is reported as released first.</summary>
    public void Follow(int virtualKey, Action<bool> changed, ITimer timer)
    {
        lock (_state)
        {
            Release();
            _timer = timer;
            _watch = new Watch(virtualKey, changed);
        }
    }

    /// <summary>Reports a held key as released; the watch stays.</summary>
    public void ReleaseNow()
    {
        lock (_state)
        {
            Release();
        }
    }

    /// <summary>Releases and ends the watch; returns the timer for the caller to dispose (outside the lock).</summary>
    public ITimer? Clear()
    {
        lock (_state)
        {
            Release();
            _watch = null;
            var timer = _timer;
            _timer = null;
            return timer;
        }
    }

    public void OnKey(int virtualKey, uint scanCode, bool extended, bool down, bool up, bool injected = false)
    {
        var fake = HoldKeyCodes.IsFake(scanCode, extended, injected);
        virtualKey = HoldKeyCodes.Sided(virtualKey, scanCode, extended);
        lock (_state)
        {
            if (_watch is not { } watch || virtualKey != watch.VirtualKey)
            {
                return;
            }

            if (fake)
            {
                // Windows' own Shift up (NumLock) also sets the key state to up while the user
                // still holds Shift: the check must not take that for a missed release. If the
                // check released it anyway (fake up held too long), Windows' fake down afterwards
                // means the key is still held: engage again, Shift does not repeat after another key.
                if (down && _fakeReleased && !_held)
                {
                    _fakeReleased = false;
                    Press(watch);
                    return;
                }

                _fakeUp = up || (_fakeUp && !down);
                _fakeUpChecks = 0;
                return;
            }

            _fakeUp = false;
            _fakeUpChecks = 0;
            _fakeReleased = false;

            // Key repeat sends further "down" events while held; only the first one counts.
            if (down && !_held)
            {
                Press(watch);
            }
            else if (up && _held)
            {
                Release();
            }
        }
    }

    /// <summary>True if the key counted as held but is physically up: a release the hook never saw.</summary>
    public bool ReleaseIfMissed()
    {
        lock (_state)
        {
            if (_watch is not { } watch || !_held || isDown(watch.VirtualKey))
            {
                _fakeUpChecks = 0;
                return false;
            }

            // The first check runs half a second after the press, long after Windows set the key
            // state: one "not down" is a release the hook never saw. After a fake up (NumLock
            // Shift, AltGr) the state may read up while the key is held, but only for a while:
            // the pointer must never stay locked, so a few seconds of "up" release it anyway.
            if (_fakeUp && ++_fakeUpChecks < FakeUpChecks)
            {
                return false;
            }

            var afterFake = _fakeUp;
            Release();
            _fakeReleased = afterFake;
            return true;
        }
    }

    /// <summary>Failed notifications since the last call.</summary>
    public int TakeFailures() => Interlocked.Exchange(ref _failures, 0);

    // Under _state.
    private void Press(Watch watch)
    {
        _held = true;
        _timer?.Change(checkInterval, checkInterval);
        Notify(watch, true);
    }

    // Under _state.
    private void Release()
    {
        // A fake up from before (e.g. before a desktop switch) must not delay the next check.
        _fakeUp = false;
        _fakeUpChecks = 0;
        _fakeReleased = false;
        if (!_held)
        {
            return;
        }

        _held = false;

        // One more check later, only to report a failed release notification; then it stops.
        _timer?.Change(checkInterval, Timeout.InfiniteTimeSpan);
        if (_watch is { } watch)
        {
            Notify(watch, false);
        }
    }

    private void Notify(Watch watch, bool held)
    {
        try
        {
            watch.Changed(held);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // An exception escaping a hook callback would end ZoomIN; the check reports it.
            Interlocked.Increment(ref _failures);
        }
    }

    private sealed record Watch(int VirtualKey, Action<bool> Changed);
}
