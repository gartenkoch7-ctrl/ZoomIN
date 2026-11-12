using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Localization;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Shell;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Freeze;

/// <summary>
/// Ctrl+Alt+N (G2): the pointer goes to the next free monitor along the layout (<see cref="MonitorCycle"/>),
/// to its last position there or else the middle. Moving the pointer is an explicit command
/// (ADR-0028). Also keeps the last positions for the jump when a monitor is frozen.
/// </summary>
public sealed class MonitorSwitchController(
    ZoomModel zoom, IMonitorProvider monitors, ICursorSource cursor, IPointerMover mover, IAnnouncer announcer, ILocalizer texts,
    ILogger<MonitorSwitchController> log)
{
    private readonly Lock _gate = new();
    private readonly PointerMemory _memory = new();
    private CycleDirection _direction = CycleDirection.Right;

    /// <summary>The switch could not be carried out; the localised hint for a tray notice.</summary>
    public event Action<string>? Refused;

    /// <summary>Ctrl+Alt+N, on the hotkey thread.</summary>
    public void NextMonitor()
    {
        lock (_gate)
        {
            Switch();
        }
    }

    /// <summary>Remembers where the pointer leaves a monitor (also when freezing moves it away).</summary>
    internal void Remember(string monitorId, PhysicalPoint position)
    {
        lock (_gate)
        {
            _memory.Remember(monitorId, position);
        }
    }

    /// <summary>
    /// Where the pointer lands on <paramref name="target"/>: its last position there, else the middle,
    /// moved into the allowed area if <paramref name="forbidden"/> covers it.
    /// </summary>
    internal PhysicalPoint LandingPoint(MonitorLayout layout, int target, IReadOnlyList<PhysicalRect> forbidden)
    {
        lock (_gate)
        {
            return Landing(layout, target, forbidden);
        }
    }

    /// <summary>Forgets monitors that are gone (also done at every switch).</summary>
    internal void KeepOnly(IReadOnlyCollection<string> monitorIds)
    {
        lock (_gate)
        {
            _memory.KeepOnly(monitorIds);
        }
    }

    private void Switch()
    {
        var layout = MonitorLayout.Read(monitors);
        if (layout.Count < 2)
        {
            // Empty: Windows cannot describe a monitor right now (display change under way).
            Refuse(layout.Count == 0 ? "layout_unavailable" : "one_monitor", layout.Count == 0 ? "Freeze.LayoutUnavailable" : "Switch.OneMonitor");
            return;
        }

        var index = cursor.TryGetPosition(out var pointer) ? layout.IndexAt(pointer) : -1;
        var frozen = zoom.Frozen.State;
        var direction = _direction;
        var next = MonitorCycle.Next(layout.Bounds, layout.FrozenMask(frozen), index, ref direction, out var skipped);
        if (next < 0)
        {
            Refuse("no_free_monitor", "Switch.NoFree");
            return;
        }

        _memory.KeepOnly(monitors.MonitorIds()); // all monitors Windows lists, also one without an area just now
        var from = index >= 0 ? layout.Ids[index] : string.Empty;
        if (index >= 0)
        {
            _memory.Remember(from, pointer);
        }

        var to = layout.Ids[next];
        var point = Landing(layout, next, frozen.Areas());
        using var operation = Telemetry.Source.StartActivity("zoomin.view.monitor_switch")?
            .SetTag("zoomin.monitor.from", from)
            .SetTag("zoomin.monitor.to", to)
            .SetTag("zoomin.monitor.skipped", skipped);
        if (!mover.MoveTo(point, PointerMoveReason.NextMonitor))
        {
            operation?.SetStatus(ActivityStatusCode.Error, "refused");
            Refuse("move_refused", "Freeze.MoveRefused");
            return;
        }

        _direction = direction;
        log.MonitorSwitched(from, to, skipped);
        announcer.Announce(texts.Format("Switch.Done", MonitorNames.Label(to)));
    }

    // Under _gate (held by the caller).
    private PhysicalPoint Landing(MonitorLayout layout, int target, IReadOnlyList<PhysicalRect> forbidden) =>
        FenceGeometry.Create(layout.Bounds, forbidden).Nearest(_memory.Recall(layout.Ids[target], layout.Bounds[target]));

    private void Refuse(string reason, string textKey)
    {
        log.SwitchRefused(reason);
        var text = texts.Get(textKey);
        announcer.Announce(text);
        Refused?.Invoke(text);
    }
}
