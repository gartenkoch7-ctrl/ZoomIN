using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZoomIN.App.Settings;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;

namespace ZoomIN.App.Pointer;

/// <summary>
/// Mouse buffer and axis lock (A6): turns the global settings into move filters of the pointer
/// fence and the held key into the axis lock. Filters are only registered while switched on, so
/// with both off no hook runs (behaviour as in Windows). Re-reads the monitors after a display
/// change and starts the key watch over after a desktop switch, so a missed release never keeps
/// the pointer on one axis.
/// </summary>
public sealed class PointerConstraintController : IDisposable
{
    private readonly IPointerFence _fence;
    private readonly IMonitorProvider _monitors;
    private readonly IHoldKeyWatch _keys;
    private readonly SettingsService _settings;
    private readonly ILogger _log;
    private readonly EdgeBuffer _buffer;
    private readonly AxisLock _axis = new();
    private readonly Action<bool> _setHeld;
    private readonly Lock _gate = new();

    // Only around the buffer's monitors and level, never around a fence call: the fence's watcher
    // thread takes it in OnRecomputed while Apply may hold _gate and wait for that thread.
    private readonly Lock _configure = new();
    private EdgeBufferLevel _bufferLevel;
    private IReadOnlyList<PhysicalRect> _lastBounds = [];
    private PointerConstraintOptions? _applied;
    private bool _bufferOn;
    private bool _axisOn;
    private bool _started;
    private bool _disposed;

    public PointerConstraintController(
        IPointerFence fence, IMonitorProvider monitors, IHoldKeyWatch keys, SettingsService settings, TimeProvider time,
        ILogger<PointerConstraintController> log)
    {
        _fence = fence;
        _monitors = monitors;
        _keys = keys;
        _settings = settings;
        _log = log;
        _buffer = new EdgeBuffer(time);
        _setHeld = _axis.SetHeld;
    }

    internal EdgeBuffer Buffer => _buffer;

    internal AxisLock Axis => _axis;

    public void Start()
    {
        lock (_gate)
        {
            if (_started)
            {
                return;
            }

            _started = true;
        }

        _settings.Changed += OnSettingsChanged;
        _fence.Recomputed += OnRecomputed;
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
                ApplyAxisLock(wanted, _applied);
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
        _settings.Changed -= OnSettingsChanged;
        _fence.Recomputed -= OnRecomputed;
        lock (_gate)
        {
            _disposed = true;
            TurnOff();
        }
    }

    private int ApplyBuffer(EdgeBufferLevel level)
    {
        if (level == EdgeBufferLevel.Off)
        {
            if (_bufferOn)
            {
                _bufferOn = false;
                _fence.RemoveFilter(_buffer);
                ConfigureBuffer(EdgeBufferLevel.Off);
            }

            return 0;
        }

        // Registered even with one monitor: only a registered filter keeps the fence watching
        // display changes, which brings the buffer back when a second monitor is plugged in.
        var monitors = ConfigureBuffer(level);
        if (!_bufferOn)
        {
            _bufferOn = true;
            _fence.AddFilter(_buffer);
        }

        return monitors;
    }

    /// <summary>Sets the level and reads the monitors; with <paramref name="level"/> null the current level is refreshed.</summary>
    private int ConfigureBuffer(EdgeBufferLevel? level)
    {
        lock (_configure)
        {
            _bufferLevel = level ?? _bufferLevel;
            if (_bufferLevel != EdgeBufferLevel.Off && _monitors.AllBounds() is { Count: > 0 } bounds)
            {
                _lastBounds = bounds;
            }

            // Empty while Windows is still enumerating after a display change: keep the last known
            // monitors rather than switching the buffer off until the next change.
            _buffer.Configure(_lastBounds, _bufferLevel);
            return _lastBounds.Count;
        }
    }

    private void ApplyAxisLock(PointerConstraintOptions wanted, PointerConstraintOptions? before)
    {
        if (!wanted.AxisLock)
        {
            if (_axisOn)
            {
                _axisOn = false;
                _keys.Stop();
                _fence.RemoveFilter(_axis);
                _axis.SetHeld(false);
            }

            return;
        }

        // Marked on first: if a step below throws, TurnOff stops the watch and removes the filter.
        var wasOn = _axisOn;
        _axisOn = true;
        if (!wasOn || before?.AxisLockKey != wanted.AxisLockKey)
        {
            _keys.Start(wanted.AxisLockKey, _setHeld);
        }

        if (!wasOn)
        {
            _fence.AddFilter(_axis);
        }
    }

    // Under the lock. Each step on its own: one failing must not keep the other running.
    private void TurnOff()
    {
        Volatile.Write(ref _applied, null);
        if (_bufferOn)
        {
            _bufferOn = false;
            Try(() => _fence.RemoveFilter(_buffer));
        }

        if (_axisOn)
        {
            _axisOn = false;
            Try(_keys.Stop);
            Try(() => _fence.RemoveFilter(_axis));
        }

        _axis.SetHeld(false);
        Try(() => ConfigureBuffer(EdgeBufferLevel.Off));
    }

    private void Try(Action step)
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
    // the watcher). The buffer is refreshed under _configure with the level Apply set last; Reset
    // is thread-safe and harmless if the axis lock was just switched off.
    private void OnRecomputed(object? sender, FenceTrigger trigger)
    {
        try
        {
            var monitors = ConfigureBuffer(null);

            // Windows did not deliver key events meanwhile: start over; a key still held
            // re-engages with its next repeat.
            if (Volatile.Read(ref _applied) is { AxisLock: true } && trigger is FenceTrigger.DesktopSwitch or FenceTrigger.SessionUnlock)
            {
                _keys.Reset();
            }

            _log.Reapplied(trigger, monitors);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.ReapplyFailed(ex, trigger);
        }
    }
}
