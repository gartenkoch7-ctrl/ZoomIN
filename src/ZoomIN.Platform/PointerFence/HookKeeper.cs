using Microsoft.Extensions.Logging;

namespace ZoomIN.Platform.PointerFence;

/// <summary>
/// The fence's mouse hook: installs it, notes a refusal (logged once per stretch) and paces the
/// retries (1, 2, 4 … 64 checks: each attempt starts and joins a thread under the fence's lock).
/// Used under the fence's lock.
/// </summary>
internal sealed class HookKeeper(ILogger log, IFenceNative native)
{
    private IDisposable? _hook;
    private bool _failureLogged;
    private int _retryWait;
    private int _retryCountdown;

    /// <summary>A refused hook waits for <see cref="TryRetry"/>.</summary>
    public bool Pending { get; private set; }

    /// <summary>Counts hooks really installed; the fence resets its heartbeat only for a new one.</summary>
    public int Installs { get; private set; }

    /// <summary>Keeps the running hook or installs one.</summary>
    /// <returns>False if Windows refused; a retry is then scheduled.</returns>
    public bool Ensure(IFenceHookTarget target) => _hook is not null || Install(target);

    /// <summary>Replaces a hook Windows seems to have removed.</summary>
    public bool Reinstall(IFenceHookTarget target)
    {
        Remove();
        return Install(target);
    }

    /// <summary>The check's retry of a refused hook, when its turn in the backoff has come.</summary>
    public bool TryRetry(IFenceHookTarget target) => Pending && --_retryCountdown <= 0 && Install(target);

    /// <summary>Keeps retrying a refused hook after the rest of the fence was released.</summary>
    public void KeepPending() => Pending = true;

    /// <summary>Removes the hook (if any) and forgets a pending retry.</summary>
    public void Remove()
    {
        Pending = false;
        var hook = _hook;
        _hook = null;
        hook?.Dispose();
    }

    private bool Install(IFenceHookTarget target)
    {
        _hook = native.InstallMoveHook(target);
        if (_hook is null)
        {
            if (!_failureLogged)
            {
                _failureLogged = true;
                log.HookUnavailable();
            }

            // The countdown starts one higher, so the check that saw the refusal does not retry at once.
            Pending = true;
            _retryWait = Math.Min(Math.Max(1, _retryWait * 2), 64);
            _retryCountdown = _retryWait + 1;
            return false;
        }

        Installs++;
        Pending = false;
        _failureLogged = false;
        _retryWait = 0;
        _retryCountdown = 0;
        return true;
    }
}
