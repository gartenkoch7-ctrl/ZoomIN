using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZoomIN.App.Settings;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.PointerFence;

namespace ZoomIN.App.Pointer;

/// <summary>
/// Mouse buffer and axis lock (A6): turns the global settings into move filters of the pointer
/// fence and the held key into the axis lock (<see cref="AxisLockSetup"/>). Filters are only
/// registered while switched on, so with both off no hook runs (behaviour as in Windows).
/// Re-reads the monitors after a display change (<see cref="BufferSetup"/>) and lets the key start
/// over after a desktop switch, so a missed release never keeps the pointer on one axis.
/// </summary>
public sealed class PointerConstraintController : IDisposable
{
    private readonly IPointerFence _fence;
    private readonly SettingsService _settings;
    private readonly ILogger _log;
    private readonly EdgeBuffer _buffer;
    private readonly BufferSetup _bufferSetup;
    private readonly AxisLockSetup _axis;
    private readonly Lock _gate = new();
    private PointerConstraintOptions? _applied;
    private bool _bufferOn;
    private bool _started;
    private bool _disposed;

    public PointerConstraintController(
        IPointerFence fence, IMonitorProvider monitors, IHoldKeyWatch keys, SettingsService settings, TimeProvider time,
        ILogger<PointerConstraintController> log)
    {
        _fence = fence;
        _settings = settings;
        _log = log;
        _buffer = new EdgeBuffer(time);
        _bufferSetup = new BufferSetup(_buffer, monitors, time, log);
        _axis = new AxisLockSetup(fence, keys, log);
    }

    internal EdgeBuffer Buffer => _buffer;

    internal AxisLock Axis => _axis.Axis;

    public void Start()
    {
        lock (_gate)
        {
            if (_started || _disposed)
            {
                return;
            }

            // Under the lock, so a concurrent Dispose always sees and removes them.
            _started = true;
            _settings.Changed += OnSettingsChanged;
            _fence.Recomputed += OnRecomputed;
        }

        Apply(_settings.Current.PointerConstraint);
    }

    /// <summary>Applies <paramref name="options"/> if they differ from the running ones; internal for tests.</summary>
    internal void Apply(PointerConstraintOptions? options)
    {
        var wanted = PointerConstraintOptions.Normalize(options);
        lock (_gate)
        {
            // After Dispose a late settings change must not install anything again.
            if (_disposed || wanted == _applied)
            {
                return;
            }

            using var activity = Telemetry.Source.StartActivity("zoomin.pointer.constraint");
            try
            {
                var monitors = ApplyBuffer(wanted.Buffer);
                _axis.Apply(wanted, _applied);
                Volatile.Write(ref _applied, wanted);
                _log.Applied(wanted.Buffer, wanted.AxisLock, wanted.AxisLockKey, monitors);
                activity?.SetTag("zoomin.buffer", wanted.Buffer.ToString())
                    .SetTag("zoomin.axis_lock", wanted.AxisLock)
                    .SetTag("zoomin.monitors", monitors);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Better no constraint than a half-applied one; the next settings change tries again.
                _log.ApplyFailed(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
                TurnOff();
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            Volatile.Write(ref _disposed, true);
            _settings.Changed -= OnSettingsChanged;
            _fence.Recomputed -= OnRecomputed;
            TurnOff();
            _bufferSetup.Dispose();
        }
    }

    // "On" is cleared only after the steps that can fail, so TurnOff still cleans up after a failure.
    private int ApplyBuffer(EdgeBufferLevel level)
    {
        if (level == EdgeBufferLevel.Off)
        {
            if (_bufferOn)
            {
                _fence.RemoveFilter(_buffer);
                _bufferOn = false;
            }

            // Also read while off: the log reports the real number of monitors.
            return _bufferSetup.Configure(EdgeBufferLevel.Off);
        }

        // Registered even with one monitor: only a registered filter keeps the fence watching
        // display changes, which brings the buffer back when a second monitor is plugged in.
        var monitors = _bufferSetup.Configure(level);
        if (!_bufferOn)
        {
            _bufferOn = true;
            _fence.AddFilter(_buffer);
        }

        return monitors;
    }

    // Under the lock. Each step on its own: one failing must not keep the other running.
    private void TurnOff()
    {
        Volatile.Write(ref _applied, null);
        if (_bufferOn)
        {
            _bufferOn = false;
            Attempt(() => _fence.RemoveFilter(_buffer));
        }

        _axis.TurnOff();
        Attempt(() => _bufferSetup.Configure(EdgeBufferLevel.Off));
    }

    private void Attempt(Action step)
    {
        try
        {
            step();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.ApplyFailed(ex);
        }
    }

    private void OnSettingsChanged() => Apply(_settings.Current.PointerConstraint);

    // On the fence's watcher thread: an escaping exception would end ZoomIN. Never takes _gate:
    // Apply holds it while the fence may join this very thread (a removed last filter retires
    // the watcher). The buffer is refreshed by BufferSetup with the level Apply set last; the key
    // reset is thread-safe and harmless if the axis lock was just switched off.
    private void OnRecomputed(object? sender, FenceTrigger trigger)
    {
        try
        {
            // The event may already have been raised when Dispose unsubscribed.
            if (Volatile.Read(ref _disposed))
            {
                return;
            }

            var monitors = _bufferSetup.Configure(null);
            if (Volatile.Read(ref _applied) is { AxisLock: true } && trigger is FenceTrigger.DesktopSwitch or FenceTrigger.SessionUnlock)
            {
                _axis.KeysMissed();
            }

            _log.Reapplied(trigger, monitors);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.ReapplyFailed(ex, trigger);
        }
    }
}
