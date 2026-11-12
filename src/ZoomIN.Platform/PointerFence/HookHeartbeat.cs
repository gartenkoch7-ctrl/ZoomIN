using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.PointerFence;

/// <summary>
/// Notices a mouse hook Windows removed silently (LowLevelHooksTimeout): the pointer moved
/// between checks, but the hook was not called once, twice in a row. A single SetCursorPos of
/// another program or of ZoomIN's own mover (which bypasses low-level hooks) is not enough.
/// Also counts filter failures from the hook, which may not log.
/// </summary>
internal sealed class HookHeartbeat
{
    private const int SilentChecks = 2;

    private long _calls;
    private long _seenCalls;
    private int _silentChecks;
    private bool _awaitBeat;
    private PhysicalPoint? _seenCursor;
    private long _failures;
    private long _reportedFailures;

    /// <summary>From the hook, per move: no allocation.</summary>
    public void Beat() => Interlocked.Increment(ref _calls);

    /// <summary>From the hook: a filter threw, the move was let through.</summary>
    public void FilterFailed() => Interlocked.Increment(ref _failures);

    /// <summary>Called by the periodic check with the current pointer position.</summary>
    public bool IsSilent(PhysicalPoint? cursor)
    {
        var calls = Interlocked.Read(ref _calls);
        var silent = cursor is { } now && _seenCursor is { } before && now != before && calls == _seenCalls;
        if (_awaitBeat && calls != _seenCalls)
        {
            _awaitBeat = false;
        }

        _silentChecks = silent ? _silentChecks + 1 : 0;
        _seenCalls = calls;
        _seenCursor = cursor;

        // After one reinstall, wait for the hook to be called again before trying once more: input
        // the hook can never see (an elevated window in front, touch, SetCursorPos tools) would
        // otherwise reinstall it every two seconds for nothing.
        return !_awaitBeat && _silentChecks >= SilentChecks;
    }

    /// <summary>A new hook was installed because this one seemed gone.</summary>
    public void Reinstalled()
    {
        Reset();
        _awaitBeat = true;
    }

    /// <summary>Filter failures since the last call; 0 if none.</summary>
    public long TakeFailures()
    {
        var failures = Interlocked.Read(ref _failures);
        var news = failures - _reportedFailures;
        _reportedFailures = failures;
        return news;
    }

    /// <summary>Forget the last position, e.g. after a new hook was installed.</summary>
    /// <remarks>Keeps a pending reinstall backoff: a new request does not make unseen input visible.</remarks>
    public void Reset()
    {
        _seenCursor = null;
        _silentChecks = 0;
    }
}
