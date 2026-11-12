using Microsoft.Extensions.Logging;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.PointerFence;

/// <summary>
/// Owns what the fence holds in Windows: the ClipCursor rectangle, the mouse hook and the
/// watcher. Not thread-safe: <see cref="Win32PointerFence"/> calls it under its lock, except
/// <see cref="ReleaseForExit"/>. Every way out releases both hook and clip, so a failure leaves
/// the pointer free, never trapped.
/// </summary>
internal sealed class FenceEnforcer(ILogger log, IFenceNative native)
{
    private volatile FenceMode _mode;
    private volatile bool _clipOwned;
    private IDisposable? _hook;
    private IDisposable? _watcher;
    private PhysicalRect? _clip;
    private bool _clipFailureLogged;
    private bool _foreignLogged;

    public FenceMode Mode => _mode;

    /// <summary>Enforces <paramref name="decision"/>; returns a retired watcher to dispose after leaving the lock.</summary>
    public IDisposable? Apply(FenceDecision decision, IFenceHookTarget target, IFenceEvents events)
    {
        if (decision.Mode == FenceMode.Off)
        {
            return Release();
        }

        if (decision.Mode == FenceMode.Clip)
        {
            DisposeHook();
            _clip = Round(decision.Geometry!.SingleRect!.Value);
            _mode = FenceMode.Clip;
            TryClip(_clip.Value);
            EnsureWatcher(events);
            return null;
        }

        Unclip();
        _clip = null;
        _hook ??= native.InstallMoveHook(target);
        if (_hook is null)
        {
            log.HookUnavailable();
            return Release();
        }

        _mode = FenceMode.Hook;
        EnsureWatcher(events);
        return null;
    }

    /// <summary>The periodic check: true if ClipCursor had been reset and was set again.</summary>
    public bool CheckClip()
    {
        if (_mode != FenceMode.Clip || _clip is not { } wanted)
        {
            return false;
        }

        var now = native.CurrentClip();
        if (now == wanted)
        {
            return false;
        }

        if (now is null || now == native.VirtualScreen())
        {
            // Windows dropped the clip (another desktop, some activations): ours again.
            return TryClip(wanted);
        }

        // A game or remote tool confines the pointer itself: leave it, and do not release its clip later.
        _clipOwned = false;
        if (!_foreignLogged)
        {
            _foreignLogged = true;
            log.ForeignClip();
        }

        return false;
    }

    /// <summary>Hook off and clip off even if one of them fails; never throws.</summary>
    /// <returns>The watcher to dispose after leaving the lock.</returns>
    public IDisposable? ReleaseSafely(FenceTrigger trigger)
    {
        var watcher = _watcher;
        try
        {
            return Release();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.UpdateFailed(ex, trigger);
            _watcher = null;
            return watcher;
        }
    }

    /// <summary>Process exit or unhandled exception: give the pointer back without a lock.</summary>
    public void ReleaseForExit() => Unclip();

    private IDisposable? Release()
    {
        _mode = FenceMode.Off;
        _clip = null;
        var watcher = _watcher;
        _watcher = null;
        try
        {
            DisposeHook();
        }
        finally
        {
            Unclip();
        }

        return watcher;
    }

    private bool TryClip(PhysicalRect rect)
    {
        // Marked first: whatever happens, a later release clears it.
        _clipOwned = true;
        if (native.ClipTo(rect, out var error))
        {
            _clipFailureLogged = false;
            _foreignLogged = false;
            return true;
        }

        if (!_clipFailureLogged)
        {
            _clipFailureLogged = true;
            log.ClipFailed(error);
        }

        return false;
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

    private void DisposeHook()
    {
        var hook = _hook;
        _hook = null;
        hook?.Dispose();
    }

    private void Unclip()
    {
        if (_clipOwned)
        {
            _clipOwned = false;
            native.Unclip();
        }
    }

    // ClipCursor and GetClipCursor work in whole pixels; rounded outwards, the comparison in CheckClip holds.
    private static PhysicalRect Round(PhysicalRect rect)
    {
        var left = Math.Floor(rect.X);
        var top = Math.Floor(rect.Y);
        return new PhysicalRect(left, top, Math.Ceiling(rect.Right) - left, Math.Ceiling(rect.Bottom) - top);
    }
}
