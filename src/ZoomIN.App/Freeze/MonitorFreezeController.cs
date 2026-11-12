using Microsoft.Extensions.Logging;
using ZoomIN.App.Settings;
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
/// Frozen monitors (G2). Ctrl+Alt+H freezes the monitor under the pointer, or lifts every freeze if
/// one is frozen. The pointer first jumps to the nearest free monitor (ADR-0028: move, then forbid),
/// then the fence keeps it out. Desktop switches lift every freeze, a display change lifts it where
/// the monitor is gone or changed (and a still image, which the rebuilt pipeline cannot keep), so the
/// pointer is never trapped. Ctrl+Alt+N is <see cref="MonitorSwitchController"/>.
/// </summary>
public sealed class MonitorFreezeController : IDisposable
{
    private readonly Lock _gate = new();
    private readonly SettingsService _settings;
    private readonly MonitorSwitchController _switch;
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
    private readonly Action<Action> _dispatch;
    private readonly FreezeOperations _operations = new();
    private volatile bool _freezing;

    public MonitorFreezeController(
        SettingsService settings, MonitorSwitchController monitorSwitch, IMonitorProvider monitors, ICursorSource cursor, IPointerFence fence,
        IPointerMover mover, IAnnouncer announcer, ILocalizer texts, ILogger<MonitorFreezeController> log)
        : this(settings, monitorSwitch, monitors, cursor, fence, mover, announcer, texts, log, work => ThreadPool.QueueUserWorkItem(_ => work()))
    {
    }

    /// <param name="dispatch">Runs fence events off the fence's watcher thread (tests: at once).</param>
    internal MonitorFreezeController(
        SettingsService settings, MonitorSwitchController monitorSwitch, IMonitorProvider monitors, ICursorSource cursor, IPointerFence fence,
        IPointerMover mover, IAnnouncer announcer, ILocalizer texts, ILogger log, Action<Action> dispatch)
    {
        _settings = settings;
        _switch = monitorSwitch;
        _zoom = settings.Models.Zoom;
        _frozen = _zoom.Frozen;
        _overview = settings.Models.Overview;
        (_monitors, _cursor, _fence, _mover) = (monitors, cursor, fence, mover);
        (_announcer, _texts, _log, _dispatch) = (announcer, texts, log, dispatch);
        _fence.Recomputed += OnFenceRecomputed;
    }

    /// <summary>Ctrl+Alt+H did nothing; the localised hint for a tray notice.</summary>
    public event Action<string>? Refused;

    /// <summary>Ctrl+Alt+H: freezes the monitor under the pointer; if one is frozen already, lifts all.</summary>
    public void ToggleFreeze() => Guarded("toggle", () =>
    {
        if (_frozen.Any)
        {
            Lift(_ => true, "command");
            return;
        }

        _freezing = true;
        try
        {
            FreezeUnderPointer();
        }
        finally
        {
            _freezing = false;
        }
    });

    public void Dispose()
    {
        _fence.Recomputed -= OnFenceRecomputed;
        Guarded("exit", () => Lift(_ => true, "exit", announce: false));
    }

    private void FreezeUnderPointer()
    {
        var layout = MonitorLayout.Read(_monitors);
        var index = _cursor.TryGetPosition(out var pointer) ? layout.IndexAt(pointer) : -1;
        if (layout.Count < 2)
        {
            Refuse("one_monitor", "Freeze.OneMonitor");
            return;
        }

        if (index < 0)
        {
            Refuse("no_pointer", "Freeze.NoPointer");
            return;
        }

        // The overview shows 1× for a moment; frozen, its frame would stay and the view before it
        // would be lost. The user ends it first (tap again or Esc).
        var id = layout.Ids[index];
        if (_overview.Current is { } session && session.MonitorId == id)
        {
            Refuse("overview", "Freeze.Overview");
            return;
        }

        var mask = new bool[layout.Count];
        mask[index] = true;
        var target = MonitorCycle.NearestFree(layout.Bounds, mask, pointer);
        _switch.KeepOnly(layout.Ids);
        _switch.Remember(id, pointer);
        var point = _switch.LandingPoint(layout, target, [layout.Bounds[index]]);

        // ADR-0028: move first, then forbid (ClipCursor would pull the pointer onto the frozen edge).
        if (!_mover.MoveTo(point, PointerMoveReason.FreezeMonitor))
        {
            Refuse("move_refused", "Freeze.MoveRefused");
            return;
        }

        var shared = _settings.Current;
        var monitor = new FrozenMonitor(
            id, layout.Bounds[index], _zoom.TargetFor(id), _zoom.Mode, _zoom.Lens, _zoom.Dock,
            shared.FrozenContent == FrozenContent.Still, RgbColor.ParseOrDefault(shared.FrozenFrameColor, RgbColor.Cyan));
        _frozen.Freeze(monitor);
        _operations.Start(monitor);
        _fence.Forbid(FenceOwner.FrozenMonitors, _frozen.State.Areas());

        // A mouse moving on while the shortcut was pressed may have brought the pointer back before the
        // fence stood (the hook lets a pointer inside a forbidden area move freely): same command, move again.
        if (_cursor.TryGetPosition(out var now) && monitor.Bounds.Contains(now) && !_mover.MoveTo(point, PointerMoveReason.FreezeMonitor))
        {
            // Never leave the pointer on a frozen monitor, whose view neither draws nor follows it.
            Lift(m => ReferenceEquals(m, monitor), "move_refused", announce: false);
            Refuse("move_refused", "Freeze.MoveRefused");
            return;
        }

        var image = FreezeOperation.Image(monitor);
        _log.MonitorFrozen(id, monitor.Level, image, layout.Ids[target]);
        _announcer.Announce(_texts.Format("Freeze.Frozen", monitor.Number));
    }

    // Raised on the fence's watcher thread, which the fence may join while releasing (up to 2 s):
    // never wait for this controller's lock there. What ends is decided by FreezeRules.ToLift, with
    // what was frozen (or being frozen) when the event came.
    private void OnFenceRecomputed(object? sender, FenceTrigger trigger)
    {
        if (trigger is FenceTrigger.DesktopSwitch or FenceTrigger.SessionUnlock or FenceTrigger.DisplayChange)
        {
            var seen = _frozen.State;
            var freezing = _freezing;
            _dispatch(() => Guarded("recompute", () => Recheck(trigger, seen, freezing)));
        }
    }

    private void Recheck(FenceTrigger trigger, FreezeState seen, bool freezing)
    {
        // Mid-change Windows may not describe every monitor yet (empty): then the next event decides,
        // and the fence lets the pointer go meanwhile, as it never traps it.
        var monitors = trigger == FenceTrigger.DisplayChange ? _monitors.AllMonitors() : [];
        if (monitors.Count > 0)
        {
            _switch.KeepOnly([.. monitors.Select(m => m.Id)]);
        }

        var mouse = _cursor.TryGetPosition(out var position) ? position : (PhysicalPoint?)null;
        var lift = FreezeRules.ToLift(trigger, _frozen.State, seen, freezing, monitors, mouse);
        Lift(lift.Contains, trigger switch
        {
            FenceTrigger.DesktopSwitch => "desktop_switch",
            FenceTrigger.SessionUnlock => "session_unlock",
            _ => "display_change",
        });
    }

    // Unfreezes first, then frees the area: the monitor follows the pointer again before it can get there.
    private void Lift(Func<FrozenMonitor, bool> which, string reason, bool announce = true)
    {
        var removed = _frozen.Unfreeze(which);
        if (removed.Count == 0)
        {
            return;
        }

        _fence.Forbid(FenceOwner.FrozenMonitors, _frozen.State.Areas());
        _operations.Stop(removed, reason);
        _log.FreezeLifted(removed.Count, reason);
        if (announce)
        {
            _announcer.Announce(_texts.Get("Freeze.Lifted"));
        }
    }

    private void Refuse(string reason, string textKey)
    {
        _log.FreezeRefused(reason);
        var text = _texts.Get(textKey);
        _announcer.Announce(text);
        Refused?.Invoke(text);
    }

    // Commands run on the hotkey thread, fence events on the thread pool: an exception must not end
    // either, and a half-done freeze must not keep the pointer out of a monitor.
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
            _frozen.UnfreezeAll();
            _operations.StopAll("failure");
            _fence.Forbid(FenceOwner.FrozenMonitors, []);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The fence releases itself on every failure of its own; nothing more to do here.
            _log.FreezeFailed(ex, "release");
        }
    }
}
