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
    private readonly Lock _state = new();
    private Watch? _watch;
    private ITimer? _timer;
    private bool _held;
    private bool _fakeUp;
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
        lock (_state)
        {
            if (_watch is not { } watch || virtualKey != watch.VirtualKey)
            {
                return;
            }

            if (fake)
            {
                // Windows' own Shift up (NumLock) also sets the key state to up while the user
                // still holds Shift: the check must not take that for a missed release.
                _fakeUp = up || (_fakeUp && !down);
                return;
            }

            _fakeUp = false;

            // Key repeat sends further "down" events while held; only the first one counts.
            if (down && !_held)
            {
                _held = true;
                _timer?.Change(checkInterval, checkInterval);
                Notify(watch, true);
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
            // The first check runs half a second after the press, long after Windows set the key
            // state: one "not down" is a release the hook never saw.
            if (_watch is { } watch && _held && !_fakeUp && !isDown(watch.VirtualKey))
            {
                Release();
                return true;
            }

            return false;
        }
    }

    /// <summary>Failed notifications since the last call.</summary>
    public int TakeFailures() => Interlocked.Exchange(ref _failures, 0);

    // Under _state.
    private void Release()
    {
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
