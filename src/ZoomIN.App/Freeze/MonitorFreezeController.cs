using Microsoft.Extensions.Logging;
using ZoomIN.App.Settings;
using ZoomIN.Core.Localization;
using ZoomIN.Core.Modes;
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
/// the monitor is gone or changed (<see cref="FreezeRules"/>), so the pointer is never trapped.
/// Ctrl+Alt+N is <see cref="MonitorSwitchController"/>.
/// </summary>
public sealed class MonitorFreezeController : IDisposable
{
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
    private readonly FreezeOperations _operations = new();
    private readonly FreezeGuard _guard;
    private readonly FreezeFenceEvents _events;
    private readonly Action<Action> _dispatch;
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
        (_settings, _switch, _zoom) = (settings, monitorSwitch, settings.Models.Zoom);
        (_frozen, _overview) = (_zoom.Frozen, settings.Models.Overview);
        (_monitors, _cursor, _fence, _mover) = (monitors, cursor, fence, mover);
        (_announcer, _texts, _log, _dispatch) = (announcer, texts, log, dispatch);
        _guard = new FreezeGuard(fence, _frozen, _operations, log);
        _events = new FreezeFenceEvents(fence, _frozen, () => _freezing, dispatch,
            (trigger, seen, freezing) => _guard.Run("recompute", () => Recheck(trigger, seen, freezing), restoreLevelOnFailure: false));
    }

    /// <summary>Ctrl+Alt+H did nothing; the localised hint for a tray notice.</summary>
    public event Action<string>? Refused;

    /// <summary>Ctrl+Alt+H: freezes the monitor under the pointer; if one is frozen already, lifts all.</summary>
    public void ToggleFreeze() => _guard.Run("toggle", () =>
    {
        if (_frozen.Any)
        {
            Lift(_ => true, "command");
            return;
        }

        try
        {
            FreezeUnderPointer();
        }
        finally
        {
            _freezing = false;
        }
    });

    /// <summary>
    /// Lifts every freeze without a shortcut, e.g. while the shortcuts are being edited or when
    /// Ctrl+Alt+H is no longer registered: otherwise nothing could give the frozen monitor back.
    /// </summary>
    public void LiftAll(string reason) => _guard.Run("lift_all", () => Lift(_ => true, reason));

    public void Dispose()
    {
        _events.Dispose();
        _guard.Run("exit", () => Lift(_ => true, "exit", announce: false));
    }

    private void FreezeUnderPointer()
    {
        // From before the layout is read, a fence event counts this freeze as made before it (its layout
        // may be outdated); a refusal and ToggleFreeze clear the flag again.
        _freezing = true;
        var layout = MonitorLayout.Read(_monitors);
        var index = _cursor.TryGetPosition(out var pointer) ? layout.IndexAt(pointer) : -1;
        // Empty: Windows cannot describe a monitor right now (display change under way), try again later.
        var (reason, text) = layout.Count == 0 ? ("layout_unavailable", "Freeze.LayoutUnavailable")
            : layout.Count < 2 ? ("one_monitor", "Freeze.OneMonitor")
            : ("no_pointer", "Freeze.NoPointer");
        if (layout.Count < 2 || index < 0)
        {
            Refuse(reason, text);
            return;
        }

        // The overview shows 1× for a moment; frozen, its frame would stay and the view before it
        // would be lost. The user ends it first (tap again or Esc).
        var id = layout.Ids[index];
        // The docked strip reserves screen space and takes every zoom command (ZoomModel.Step): frozen,
        // the reservation would follow the live mode and zooming elsewhere would go nowhere.
        var docked = _zoom.Mode == ZoomMode.Docked && _zoom.IsOn && _zoom.DockedMonitor == id;
        if (docked || (_overview.Current is { } session && session.MonitorId == id))
        {
            Refuse(docked ? "docked" : "overview", docked ? "Freeze.Docked" : "Freeze.Overview");
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
            shared.FrozenContent == FrozenContent.Still, RgbColor.ParseOrDefault(shared.FrozenFrameColor, RgbColor.Cyan), OnPointerInside);
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

        var image = monitor.ImageKind;
        _log.MonitorFrozen(id, monitor.Level, image, layout.Ids[target]);
        _announcer.Announce(_texts.Format("Freeze.Frozen", MonitorNames.Label(id)));
    }

    // A render thread saw the pointer on a frozen monitor (the fence failed, e.g. its hook was lost):
    // checked again here, as it may have been the moment before the second move of a freeze.
    private void OnPointerInside(FrozenMonitor monitor) => _dispatch(() => _guard.Run("pointer_inside", () =>
    {
        if (_cursor.TryGetPosition(out var now) && monitor.Bounds.Contains(now))
        {
            Lift(m => ReferenceEquals(m, monitor), "pointer_inside");
        }
    }));

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
        var reason = trigger switch
        {
            FenceTrigger.DesktopSwitch => "desktop_switch",
            FenceTrigger.SessionUnlock => "session_unlock",
            _ => "display_change",
        };

        // After a display change the monitor is gone or changed: it starts at 1× rather than at its old level.
        Lift(lift.Contains, reason, restoreLevel: trigger != FenceTrigger.DisplayChange);
    }

    // Unfreezes first, then frees the area: the monitor follows the pointer again before it can get there.
    private void Lift(Func<FrozenMonitor, bool> which, string reason, bool announce = true, bool restoreLevel = true)
    {
        var removed = _frozen.Unfreeze(which, restoreLevel);
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
        _freezing = false; // nothing is being frozen any more
        _log.FreezeRefused(reason);
        var text = _texts.Get(textKey);
        _announcer.Announce(text);
        Refused?.Invoke(text);
    }
}
