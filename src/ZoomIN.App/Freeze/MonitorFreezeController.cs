using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZoomIN.App.Settings;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Localization;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Shell;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Freeze;

/// <summary>
/// Frozen monitors and the monitor switch (G2). Ctrl+Alt+H freezes the monitor under the pointer
/// (or lifts every freeze), Ctrl+Alt+N moves the pointer to the next free monitor along the layout.
/// Moving the pointer is an explicit command (ADR-0028): first <see cref="IPointerMover"/>, then the
/// fence forbids the frozen monitors. Desktop switches lift every freeze, a display change lifts it
/// where the monitor is gone or changed, so the pointer is never trapped. Commands from the hotkey
/// thread and fence events from its watcher thread run one at a time.
/// </summary>
public sealed class MonitorFreezeController : IDisposable
{
    private readonly Lock _gate = new();
    private readonly SettingsService _settings;
    private readonly ZoomModel _zoom;
    private readonly MonitorFreeze _frozen;
    private readonly OverviewModel _overview;
    private readonly IMonitorProvider _monitors;
    private readonly ICursorSource _cursor;
    private readonly IPointerFence _fence;
    private readonly IPointerMover _mover;
    private readonly IAnnouncer _announcer;
    private readonly ILocalizer _texts;
    private readonly ILogger _log;
    private readonly PointerMemory _memory = new();
    private readonly Dictionary<FrozenMonitor, Activity?> _operations = [];
    private CycleDirection _direction = CycleDirection.Right;

    public MonitorFreezeController(
        SettingsService settings, IMonitorProvider monitors, ICursorSource cursor, IPointerFence fence, IPointerMover mover,
        IAnnouncer announcer, ILocalizer texts, ILogger<MonitorFreezeController> log)
    {
        _settings = settings;
        _zoom = settings.Models.Zoom;
        _frozen = _zoom.Frozen;
        _overview = settings.Models.Overview;
        _monitors = monitors;
        _cursor = cursor;
        _fence = fence;
        _mover = mover;
        _announcer = announcer;
        _texts = texts;
        _log = log;
        _fence.Recomputed += OnFenceRecomputed;
    }

    /// <summary>A command could not be carried out; the localised hint for a tray notice.</summary>
    public event Action<string>? Refused;

    /// <summary>Ctrl+Alt+H: freezes the monitor under the pointer; if one is frozen already, lifts all.</summary>
    public void ToggleFreeze() => Guarded("toggle", () =>
    {
        if (_frozen.Any)
        {
            Lift(_ => true, "command");
        }
        else
        {
            FreezeUnderPointer();
        }
    });

    /// <summary>Ctrl+Alt+N: the pointer goes to the next free monitor (its last position there, else the middle).</summary>
    public void NextMonitor() => Guarded("next_monitor", SwitchMonitor);

    public void Dispose()
    {
        _fence.Recomputed -= OnFenceRecomputed;
        Guarded("exit", () => Lift(_ => true, "exit"));
    }

    private void FreezeUnderPointer()
    {
        var layout = MonitorLayout.Read(_monitors);
        if (layout.Count < 2)
        {
            Refuse(_log.FreezeRefused, "one_monitor", "Freeze.OneMonitor");
            return;
        }

        var index = _cursor.TryGetPosition(out var pointer) ? layout.IndexAt(pointer) : -1;
        if (index < 0)
        {
            Refuse(_log.FreezeRefused, "no_pointer", "Freeze.NoPointer");
            return;
        }

        var mask = layout.FrozenMask(_frozen.State);
        mask[index] = true;
        var target = MonitorCycle.NearestFree(layout.Bounds, mask, pointer);
        if (target < 0)
        {
            Refuse(_log.FreezeRefused, "last_free", "Freeze.LastFree");
            return;
        }

        var id = layout.Ids[index];
        PhysicalRect[] forbidden = [.. _frozen.State.Areas(), layout.Bounds[index]];
        _memory.Remember(id, pointer);
        var point = FenceGeometry.Create(layout.Bounds, forbidden).Nearest(_memory.Recall(layout.Ids[target], layout.Bounds[target]));

        // The overview of this monitor would keep running unseen (its Esc swallowed): end it first.
        if (_overview.Current is { } session && session.MonitorId == id)
        {
            _overview.Cancel(session);
        }

        // ADR-0028: move first, then forbid (ClipCursor would pull the pointer onto the frozen edge).
        if (!_mover.MoveTo(point, PointerMoveReason.FreezeMonitor))
        {
            Refuse(_log.FreezeRefused, "move_refused", "Freeze.MoveRefused");
            return;
        }

        var shared = _settings.Current;
        var monitor = new FrozenMonitor(
            id, layout.Bounds[index], _zoom.TargetFor(id), _zoom.Mode, _zoom.Lens, _zoom.Dock,
            shared.FrozenContent == FrozenContent.Still, RgbColor.ParseOrDefault(shared.FrozenFrameColor, RgbColor.Cyan));
        _frozen.Freeze(monitor);
        _fence.Forbid(FenceOwner.FrozenMonitors, _frozen.State.Areas());
        _operations[monitor] = FreezeOperation.Start(monitor);
        var image = FreezeOperation.Image(monitor);
        _log.MonitorFrozen(id, monitor.Level, image, layout.Ids[target]);
        _announcer.Announce(_texts.Format("Freeze.Frozen", monitor.Number));
    }

    private void SwitchMonitor()
    {
        var layout = MonitorLayout.Read(_monitors);
        if (layout.Count < 2)
        {
            Refuse(_log.SwitchRefused, "one_monitor", "Switch.OneMonitor");
            return;
        }

        var hasPointer = _cursor.TryGetPosition(out var pointer);
        var index = hasPointer ? layout.IndexAt(pointer) : -1;
        var direction = _direction;
        var next = MonitorCycle.Next(layout.Bounds, layout.FrozenMask(_frozen.State), index, ref direction, out var skipped);
        if (next < 0)
        {
            Refuse(_log.SwitchRefused, "no_free_monitor", "Switch.NoFree");
            return;
        }

        var from = index >= 0 ? layout.Ids[index] : string.Empty;
        if (index >= 0)
        {
            _memory.Remember(from, pointer);
        }

        var point = FenceGeometry.Create(layout.Bounds, _frozen.State.Areas()).Nearest(_memory.Recall(layout.Ids[next], layout.Bounds[next]));
        using var operation = Telemetry.Source.StartActivity("zoomin.view.monitor_switch")?
            .SetTag("zoomin.monitor.from", from)
            .SetTag("zoomin.monitor.to", layout.Ids[next])
            .SetTag("zoomin.monitor.skipped", skipped);
        if (!_mover.MoveTo(point, PointerMoveReason.NextMonitor))
        {
            operation?.SetStatus(ActivityStatusCode.Error, "refused");
            Refuse(_log.SwitchRefused, "move_refused", "Freeze.MoveRefused");
            return;
        }

        _direction = direction;
        _log.MonitorSwitched(from, layout.Ids[next], skipped);
        _announcer.Announce(_texts.Format("Switch.Done", MonitorNames.Number(layout.Ids[next])));
    }

    // The fence recomputed on its own (watcher thread). Back from another desktop (Win+L, UAC,
    // Ctrl+Alt+Del) every freeze ends (M10 decision); after a display change only where the monitor
    // is gone or changed, and all if no free monitor would be left.
    private void OnFenceRecomputed(object? sender, FenceTrigger trigger) => Guarded("recompute", () =>
    {
        if (!_frozen.Any)
        {
            return;
        }

        switch (trigger)
        {
            case FenceTrigger.DesktopSwitch:
                Lift(_ => true, "desktop_switch");
                break;
            case FenceTrigger.SessionUnlock:
                Lift(_ => true, "session_unlock");
                break;
            case FenceTrigger.DisplayChange:
                var layout = MonitorLayout.Read(_monitors);
                _memory.KeepOnly(layout.Ids);
                Lift(m => layout.IndexOf(m.Id) is var i && (i < 0 || layout.Bounds[i] != m.Bounds), "display_change");
                if (_frozen.Any && layout.FrozenMask(_frozen.State).All(frozen => frozen))
                {
                    Lift(_ => true, "display_change");
                }

                break;
            default:
                break;
        }
    });

    // Unfreezes first, then frees the area: the monitor follows the pointer again before it can get there.
    private void Lift(Func<FrozenMonitor, bool> which, string reason)
    {
        var removed = _frozen.Unfreeze(which);
        if (removed.Count == 0)
        {
            return;
        }

        _fence.Forbid(FenceOwner.FrozenMonitors, _frozen.State.Areas());
        foreach (var monitor in removed)
        {
            FreezeOperation.Stop(_operations.Remove(monitor, out var operation) ? operation : null, reason);
        }

        _log.FreezeLifted(removed.Count, reason);
        if (reason != "exit")
        {
            _announcer.Announce(_texts.Get("Freeze.Lifted"));
        }
    }

    private void Refuse(Action<string> log, string reason, string textKey)
    {
        log(reason);
        var text = _texts.Get(textKey);
        _announcer.Announce(text);
        Refused?.Invoke(text);
    }

    // Commands run on the hotkey thread, fence events on the watcher's thread: an exception must not
    // end either, and a half-done freeze must not keep the pointer out of a monitor.
    private void Guarded(string step, Action action)
    {
        lock (_gate)
        {
            try
            {
                action();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _log.FreezeFailed(ex, step);
                ReleaseAll();
            }
        }
    }

    private void ReleaseAll()
    {
        try
        {
            foreach (var monitor in _frozen.UnfreezeAll())
            {
                FreezeOperation.Stop(_operations.Remove(monitor, out var operation) ? operation : null, "failure");
            }

            _fence.Forbid(FenceOwner.FrozenMonitors, []);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The fence releases itself on every failure of its own; nothing more to do here.
            _log.FreezeFailed(ex, "release");
        }
    }
}
