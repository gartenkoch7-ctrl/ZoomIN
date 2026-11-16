using Microsoft.Extensions.Logging;
using ZoomIN.Core.PointerFence;

namespace ZoomIN.Platform.PointerFence;

/// <summary>
/// Owns what the fence holds in Windows: the ClipCursor rectangle (<see cref="ClipKeeper"/>), the
/// mouse hook (<see cref="HookKeeper"/>) and the watcher. Not thread-safe: <see cref="Win32PointerFence"/>
/// calls it under its lock, except <see cref="ReleaseForExit"/>. Every way out releases hook and
/// clip, so a failure leaves the pointer free, never trapped.
/// </summary>
internal sealed class FenceEnforcer(ILogger log, IFenceNative native)
{
    private readonly ClipKeeper _clip = new(log, native);
    private readonly HookKeeper _hooks = new(log, native);
    private volatile FenceMode _mode;
    private IDisposable? _watcher;
    private volatile bool _exited;

    public FenceMode Mode => _mode;

    /// <summary>Counts hooks really installed; the fence resets its heartbeat only for a new one.</summary>
    public int HookInstalls => _hooks.Installs;

    /// <summary>Enforces <paramref name="decision"/>; returns a retired watcher to dispose after leaving the lock.</summary>
    /// <param name="keepWatching">
    /// Owners still have areas or filters: keep the watcher even while off, so a later display
    /// change or desktop switch can turn the fence on again.
    /// </param>
    public IDisposable? Apply(FenceDecision decision, IFenceHookTarget target, IFenceEvents events, bool keepWatching, FenceTrigger trigger)
    {
        var retired = _exited ? null : ApplyCore(decision, target, events, keepWatching, trigger);

        // The process may have begun to exit while this ran (that release takes no lock): undo.
        return _exited ? ReleaseSafely(trigger) ?? retired : retired;
    }

    /// <summary>
    /// The check's retry after Windows refused the hook: only the hook is installed again, the rest
    /// (geometry, clip) stays as it is.
    /// </summary>
    /// <returns>True if the hook runs now.</returns>
    public bool RetryHook(IFenceHookTarget target)
    {
        if (_exited || !_hooks.TryRetry(target))
        {
            return false;
        }

        _mode = FenceMode.Hook;
        return !UndoIfExited();
    }

    /// <summary>The hook seems gone (Windows removes slow hooks silently): install a new one.</summary>
    /// <returns>False if Windows refused and nothing holds the pointer any more; a clip, if any, stays (mode Clip).</returns>
    public bool ReinstallHook(IFenceHookTarget target)
    {
        if (_mode != FenceMode.Hook || _exited)
        {
            return true;
        }

        if (!_hooks.Reinstall(target))
        {
            _mode = _clip.IsWanted ? FenceMode.Clip : FenceMode.Off;
        }

        return !UndoIfExited() && _mode != FenceMode.Off;
    }

    /// <summary>The periodic check: true if ClipCursor had been dropped or cut by Windows and was set again.</summary>
    public bool CheckClip() => _mode != FenceMode.Off && _clip.Check();

    /// <summary>Hook off and clip off even if one of them fails; never throws.</summary>
    /// <param name="keepWatching">Keep the watcher (owners still want the fence); otherwise it is handed back.</param>
    /// <returns>The watcher to dispose after leaving the lock.</returns>
    public IDisposable? ReleaseSafely(FenceTrigger trigger, bool keepWatching = false, IFenceEvents? events = null)
    {
        _mode = FenceMode.Off;
        var watcher = _watcher;
        _watcher = null;
        try
        {
            _hooks.Remove();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The hook thread ends with the process at the latest; the clip goes in any case below.
            log.UpdateFailed(ex, trigger);
        }
        finally
        {
            ClearClip(trigger);
        }

        if (!keepWatching || events is null)
        {
            return watcher;
        }

        _watcher = watcher;
        EnsureWatcher(events);
        return null;
    }

    /// <summary>Process exit or unhandled exception: give the pointer back without a lock.</summary>
    public void ReleaseForExit()
    {
        _exited = true;
        _mode = FenceMode.Off;
        _clip.ReleaseForExit();
    }

    private IDisposable? ApplyCore(FenceDecision decision, IFenceHookTarget target, IFenceEvents events, bool keepWatching, FenceTrigger trigger)
    {
        // One rectangle is always held with ClipCursor, also when filters need the hook as well:
        // the clip is the stronger guard (absolute input, no gap while the hook is reinstalled).
        var single = decision.Geometry?.SingleRect;
        if (decision.Mode == FenceMode.Clip)
        {
            _hooks.Remove();
            _mode = FenceMode.Clip;
            _clip.Keep(single);
            EnsureWatcher(events);
            return null;
        }

        if (decision.Mode != FenceMode.Hook)
        {
            return ReleaseSafely(trigger, keepWatching, events);
        }

        if (_hooks.Ensure(target) || single is not null)
        {
            // Without a hook only the filters are missing: a single rectangle still holds.
            _mode = _hooks.Pending ? FenceMode.Clip : FenceMode.Hook;
            _clip.Keep(single);
            EnsureWatcher(events);
            return null;
        }

        var retired = ReleaseSafely(trigger, keepWatching, events);
        _hooks.KeepPending();
        return retired;
    }

    // ReleaseForExit takes no lock: if it ran meanwhile, nothing installed here may stay.
    private bool UndoIfExited()
    {
        if (!_exited)
        {
            return false;
        }

        _mode = FenceMode.Off;
        _hooks.Remove();
        return true;
    }

    private void ClearClip(FenceTrigger trigger)
    {
        try
        {
            _clip.Clear();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.UpdateFailed(ex, trigger);
        }
    }

    private void EnsureWatcher(IFenceEvents events)
    {
        if (_watcher is not null)
        {
            return;
        }

        try
        {
            _watcher = native.StartWatcher(events);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The fence still works; Windows' own resets then simply stay (the pointer is free).
            log.WatcherUnavailable(ex);
        }
    }
}
