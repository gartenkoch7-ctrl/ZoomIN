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
    private PointerConstraintOptions? _applied;
    private bool _bufferOn;
    private bool _axisOn;
    private bool _started;

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
        var wanted = (options ?? PointerConstraintOptions.Default).Normalized();
        lock (_gate)
        {
            if (wanted == _applied)
            {
                return;
            }

            using var activity = Telemetry.Source.StartActivity("zoomin.pointer.constraint");
            try
            {
                var monitors = ApplyBuffer(wanted.Buffer);
                ApplyAxisLock(wanted, _applied);
                _applied = wanted;
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
                _buffer.Configure([], EdgeBufferLevel.Off);
            }

            return 0;
        }

        var bounds = _monitors.AllBounds();
        _buffer.Configure(bounds, level);
        if (!_bufferOn)
        {
            _fence.AddFilter(_buffer);
            _bufferOn = true;
        }

        return bounds.Count;
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
            }

            return;
        }

        if (!_axisOn || before?.AxisLockKey != wanted.AxisLockKey)
        {
            _keys.Start(wanted.AxisLockKey, _setHeld);
        }

        if (!_axisOn)
        {
            _fence.AddFilter(_axis);
            _axisOn = true;
        }
    }

    // Under the lock. Each step on its own: one failing must not keep the other running.
    private void TurnOff()
    {
        _applied = null;
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
        _buffer.Configure([], EdgeBufferLevel.Off);
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

    // On the fence's watcher thread: an escaping exception would end ZoomIN.
    private void OnRecomputed(object? sender, FenceTrigger trigger)
    {
        try
        {
            lock (_gate)
            {
                if (_applied is not { } applied)
                {
                    return;
                }

                var monitors = 0;
                if (_bufferOn)
                {
                    var bounds = _monitors.AllBounds();
                    _buffer.Configure(bounds, applied.Buffer);
                    monitors = bounds.Count;
                }

                // Windows did not deliver key events meanwhile: start over; a key still held
                // re-engages with its next repeat.
                if (_axisOn && trigger is FenceTrigger.DesktopSwitch or FenceTrigger.SessionUnlock)
                {
                    _keys.Start(applied.AxisLockKey, _setHeld);
                }

                _log.Reapplied(trigger, monitors);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.ReapplyFailed(ex, trigger);
        }
    }
}
