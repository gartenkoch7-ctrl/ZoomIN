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
public sealed class Win32PointerFence : IPointerFence, IFenceHookTarget, IFenceEvents
{
    private readonly ILogger _log;
    private readonly IMonitorProvider _monitors;
    private readonly FenceEnforcer _enforcer;
    private readonly TimeProvider _time;
    private readonly bool _watchesProcess;
    private readonly Lock _gate = new();
    private readonly Dictionary<FenceOwner, PhysicalRect[]> _forbidden = [];
    private readonly List<IPointerMoveFilter> _filters = [];

    // Read by the mouse hook without a lock; replaced as a whole.
    private FenceDecision _decision = FenceDecision.Open;
    private long _filterFailures;
    private long _reportedFailures;
    private int _areaCount;
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
        _time = time;
        _watchesProcess = watchProcess;
        if (watchProcess)
        {
            // A crash or Environment.Exit skips Dispose; the hook goes with the process, ClipCursor
            // would stay. FailFast skips even these handlers: the watchdog's cleanup covers that.
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            AppDomain.CurrentDomain.UnhandledException += OnProcessExit;
        }
    }

    public event EventHandler<FenceTrigger>? Recomputed;

    public FenceMode Mode => _enforcer.Mode;

    public void Forbid(FenceOwner owner, IReadOnlyList<PhysicalRect> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);
        var copy = areas.ToArray();
        Change(() =>
        {
            if (copy.Length == 0)
            {
                _forbidden.Remove(owner);
            }
            else
            {
                _forbidden[owner] = copy;
            }
        });
    }

    public void AddFilter(IPointerMoveFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        Change(() =>
        {
            if (!_filters.Contains(filter))
            {
                _filters.Add(filter);
            }
        });
    }

    public void RemoveFilter(IPointerMoveFilter filter) => Change(() => _filters.Remove(filter));

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
            Volatile.Write(ref _decision, FenceDecision.Open);
            retired = _enforcer.ReleaseSafely(FenceTrigger.Dispose);
            if (wasOn)
            {
                _log.FenceChanged(FenceMode.Off, FenceTrigger.Dispose, 0, 0);
            }
        }

        if (_watchesProcess)
        {
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            AppDomain.CurrentDomain.UnhandledException -= OnProcessExit;
        }

        retired?.Dispose();
    }

    /// <summary>Mouse hook thread, per move: lock-free, no allocation, never throws.</summary>
    bool IFenceHookTarget.OnMove(PhysicalPoint current, PhysicalPoint requested, out PhysicalPoint allowed)
    {
        var decision = Volatile.Read(ref _decision);
        try
        {
            allowed = decision.Decide(_time.GetTimestamp(), current, requested);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // A broken filter must not stop the pointer; reported by the next check.
            Interlocked.Increment(ref _filterFailures);
            allowed = requested;
            return false;
        }

        if (allowed == requested)
        {
            return false;
        }

        FenceMetrics.Blocked();
        return true;
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
            if (_disposed)
            {
                return;
            }

            var failures = Interlocked.Read(ref _filterFailures);
            if (failures != _reportedFailures)
            {
                _log.FilterFailures(failures - _reportedFailures);
                _reportedFailures = failures;
            }

            try
            {
                if (_enforcer.CheckClip())
                {
                    FenceMetrics.Changed(FenceMode.Clip, FenceTrigger.ClipLost);
                    _log.FenceChanged(FenceMode.Clip, FenceTrigger.ClipLost, _areaCount, 0);
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _log.UpdateFailed(ex, FenceTrigger.ClipLost);
                Volatile.Write(ref _decision, FenceDecision.Open);
                retired = _enforcer.ReleaseSafely(FenceTrigger.ClipLost);
            }
        }

        retired?.Dispose();
    }

    /// <summary>Process exit or unhandled exception: give the pointer back without waiting for anything.</summary>
    internal void ReleaseForExit() => _enforcer.ReleaseForExit();

    private void OnProcessExit(object? sender, EventArgs e) => ReleaseForExit();

    // Changes the state and enforces it. The retired watcher is disposed after leaving the lock:
    // it joins its thread, which may be waiting for this lock in OnTrigger.
    private bool Change(Action? change, FenceTrigger trigger = FenceTrigger.Request)
    {
        IDisposable? retired;
        lock (_gate)
        {
            if (_disposed)
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
                Volatile.Write(ref _decision, FenceDecision.Open);
                retired = _enforcer.ReleaseSafely(trigger);
            }
        }

        retired?.Dispose();
        return true;
    }

    private IDisposable? Enforce(FenceTrigger trigger)
    {
        using var activity = Telemetry.Source.StartActivity("zoomin.pointer.fence");
        var areas = _forbidden.Values.SelectMany(rects => rects).ToArray();
        _areaCount = areas.Length;
        var geometry = areas.Length == 0 ? null : FenceGeometry.Create(CurrentMonitors(), areas);
        if (geometry is { IsEmpty: true })
        {
            _log.NothingAllowed();
        }

        // Published before the hook can run, withdrawn when the enforcer ends up off.
        var decision = new FenceDecision(geometry, _filters);
        Volatile.Write(ref _decision, decision);
        var retired = _enforcer.Apply(decision, this, this);
        var mode = _enforcer.Mode;
        if (mode == FenceMode.Off)
        {
            Volatile.Write(ref _decision, FenceDecision.Open);
        }

        activity?.SetTag("zoomin.fence.mode", FenceMetrics.ModeName(mode));
        activity?.SetTag("zoomin.fence.trigger", FenceMetrics.TriggerName(trigger));
        activity?.SetTag("zoomin.fence.forbidden", areas.Length);
        FenceMetrics.Changed(mode, trigger);
        _log.FenceChanged(mode, trigger, areas.Length, decision.FilterCount);
        return retired;
    }

    private List<PhysicalRect> CurrentMonitors() =>
        [.. _monitors.MonitorIds().Select(_monitors.BoundsOf).Where(bounds => bounds.HasValue).Select(bounds => bounds!.Value)];
}
