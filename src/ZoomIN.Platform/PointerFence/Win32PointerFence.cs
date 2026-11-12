using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.PointerFence;

/// <summary>
/// The pointer fence (ADR-0028, M19 step 0): ClipCursor for one rectangle, otherwise a writing
/// low-level mouse hook (<see cref="FenceEnforcer"/>). Nothing runs while nothing is forbidden
/// and no filter is registered. Every failure releases the pointer: a fence that cannot be kept
/// is better off than one that traps (docs/kb/eingabe-und-tracking.md, "Zeigerzaun").
/// </summary>
public sealed class Win32PointerFence : IPointerFence, IFenceEvents
{
    private readonly ILogger _log;
    private readonly IMonitorProvider _monitors;
    private readonly FenceEnforcer _enforcer;
    private readonly FenceChecker _checker;
    private readonly HookHeartbeat _heartbeat = new();
    private readonly MoveGate _moves;
    private readonly ProcessExitGuard? _exitGuard;
    private readonly Lock _gate = new();
    private readonly FenceRequests _requests = new();

    private int _areaCount;
    private FenceDecision _lastDecision = FenceDecision.Open;
    private IDisposable? _retiring;
    private volatile bool _exited;
    private bool _disposed;

    public Win32PointerFence(ILogger<Win32PointerFence> log, IMonitorProvider monitors)
        : this(log, monitors, new Win32FenceNative(), TimeProvider.System, watchProcess: true)
    {
    }

    internal Win32PointerFence(ILogger log, IMonitorProvider monitors, IFenceNative native, TimeProvider time, bool watchProcess = false)
    {
        _log = log;
        _monitors = monitors;
        _enforcer = new FenceEnforcer(log, native);
        _checker = new FenceChecker(log, native, _enforcer, _heartbeat);
        _moves = new MoveGate(time, _heartbeat);
        _exitGuard = watchProcess ? new ProcessExitGuard(ReleaseForExit) : null;
    }

    public event EventHandler<FenceTrigger>? Recomputed;

    public FenceMode Mode => _enforcer.Mode;

    public void Forbid(FenceOwner owner, IReadOnlyList<PhysicalRect> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);
        var copy = areas.ToArray();
        Change(() => _requests.Forbid(owner, copy));
    }

    public void AddFilter(IPointerMoveFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        Change(() => _requests.Add(filter));
    }

    public void RemoveFilter(IPointerMoveFilter filter) => Change(() => _requests.Remove(filter));

    public void Dispose()
    {
        IDisposable? retired;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            var wasOn = _enforcer.Mode != FenceMode.Off;
            retired = Release(FenceTrigger.Dispose);
            if (wasOn)
            {
                _log.FenceChanged(FenceMode.Off, FenceTrigger.Dispose, 0, 0);
            }
        }

        _exitGuard?.Dispose();
        retired?.Dispose();
    }

    void IFenceEvents.OnTrigger(FenceTrigger trigger)
    {
        if (!Change(null, trigger))
        {
            return;
        }

        try
        {
            Recomputed?.Invoke(this, trigger);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Runs on the watcher's window thread: an escaping exception would end ZoomIN.
            _log.UpdateFailed(ex, trigger);
        }
    }

    void IFenceEvents.OnCheck()
    {
        IDisposable? retired = null;
        lock (_gate)
        {
            if (_disposed || _exited)
            {
                return;
            }

            retired = Check();
        }

        retired?.Dispose();
    }

    /// <summary>Process exit or unhandled exception: give the pointer back without the fence's lock.</summary>
    internal void ReleaseForExit()
    {
        // The hook and watcher threads may live on during a slow shutdown: let every move through
        // and stop all further changes from now on.
        _exited = true;
        _moves.CloseForExit();
        _enforcer.ReleaseForExit();
    }

    // Changes the state and enforces it. The retired watcher is disposed after leaving the lock:
    // it joins its thread, which may be waiting for this lock in OnTrigger.
    private bool Change(Action? change, FenceTrigger trigger = FenceTrigger.Request)
    {
        IDisposable? retired;
        lock (_gate)
        {
            if (_disposed || _exited)
            {
                return false;
            }

            change?.Invoke();
            try
            {
                retired = Enforce(trigger);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _log.UpdateFailed(ex, trigger);
                // A watcher Apply had already handed back must not get lost with the exception.
                var pending = TakeRetiring();
                var released = Release(trigger);
                retired = pending is not null && released is not null ? new RetiredPair(pending, released) : pending ?? released;
            }
        }

        retired?.Dispose();
        return true;
    }

    // While owners still want the fence (and it is not disposed), the watcher stays: the next
    // display change or desktop switch tries again.
    private IDisposable? Release(FenceTrigger trigger)
    {
        _moves.Open();
        var retired = _enforcer.ReleaseSafely(trigger, keepWatching: !_disposed && _requests.Any, this);
        FenceMetrics.Changed(FenceMode.Off, trigger);
        return retired;
    }

    // Under the lock, about once a second.
    private IDisposable? Check()
    {
        try
        {
            if (_checker.Run(_moves, _areaCount, _requests.Filters.Count))
            {
                _moves.Open();
            }

            // The hook was refused (now or earlier): only the hook is tried again, nothing recomputed.
            if (_enforcer.RetryHook(_moves))
            {
                _moves.Publish(_lastDecision);
                _heartbeat.Reset();
                FenceMetrics.Changed(FenceMode.Hook, FenceTrigger.HookLost);
                _log.FenceChanged(FenceMode.Hook, FenceTrigger.HookLost, _areaCount, _lastDecision.FilterCount);
            }

            return null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.UpdateFailed(ex, FenceTrigger.ClipLost);
            return Release(FenceTrigger.ClipLost);
        }
    }

    private IDisposable? Enforce(FenceTrigger trigger)
    {
        using var activity = Telemetry.Source.StartActivity("zoomin.pointer.fence");
        var areas = _requests.Areas();
        _areaCount = areas.Length;
        var geometry = areas.Length == 0 ? null : FenceGeometry.Create(_monitors.AllBounds(), areas);
        if (geometry is { IsEmpty: true })
        {
            _log.NothingAllowed();
        }

        // Published before the hook can run, withdrawn when the enforcer ends up off.
        var decision = new FenceDecision(geometry, _requests.Filters);
        _moves.Publish(decision);
        _lastDecision = decision;
        var installs = _enforcer.HookInstalls;
        _retiring = _enforcer.Apply(decision, _moves, this, keepWatching: _requests.Any, trigger);
        if (_enforcer.HookInstalls != installs)
        {
            // Only a new hook starts a new heartbeat: frequent requests must not hide a dead one.
            _heartbeat.Reset();
        }
        var mode = _enforcer.Mode;
        if (mode == FenceMode.Off)
        {
            _moves.Open();
        }

        FenceMetrics.Applied(activity, mode, trigger, areas.Length);
        _log.FenceChanged(mode, trigger, areas.Length, decision.FilterCount);
        return TakeRetiring();
    }

    private IDisposable? TakeRetiring()
    {
        var retiring = _retiring;
        _retiring = null;
        return retiring;
    }
}
