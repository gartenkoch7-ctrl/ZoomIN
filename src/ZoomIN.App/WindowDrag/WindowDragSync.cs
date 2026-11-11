using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.WindowDrag;

namespace ZoomIN.App.WindowDrag;

/// <summary>
/// Wires the drag source to <see cref="WindowDragZoom"/> (G1): finds the monitor under the pointer
/// and records each drag as the operation "zoomin.view.window_drag" from start to return. All
/// calls arrive on the source's thread, one at a time.
/// </summary>
public sealed class WindowDragSync(
    WindowDragZoom zoom, IWindowDragSource source, IMonitorProvider monitors, ICursorSource cursor, ILogger<WindowDragSync> log)
{
    private Activity? _activity;
    private IReadOnlyList<string> _monitorIds = [];
    private bool _monitorSetChanged;

    public void Start() => source.Start(OnSignal, () => zoom.CanAct);

    /// <summary>One step of a drag; internal for tests, which pass the steps by hand.</summary>
    internal void OnSignal(WindowDragSignal signal)
    {
        try
        {
            switch (signal.Stage)
            {
                case WindowDragStage.Started:
                    Started(signal.Kind);
                    break;
                case WindowDragStage.MonitorChanged:
                    MonitorChanged();
                    break;
                case WindowDragStage.Ended:
                    Finish();
                    break;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.DragFailed(ex);

            // The type, not the message (may name paths). The drag goes on; its end stops the trace.
            _activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
        }
    }

    private void Started(WindowDragKind kind)
    {
        Finish(); // a start without an end: report the old drag and give its level back
        _monitorSetChanged = false;

        // Without the pointer (e.g. secure desktop switch) the monitor is unknown: leave this drag alone.
        if (MonitorUnderPointer() is not { } monitor)
        {
            return;
        }

        _activity = Telemetry.Source.StartActivity("zoomin.view.window_drag");
        _monitorIds = monitors.MonitorIds();
        zoom.Start(kind, monitor);
    }

    // A monitor set change is noticed only when the pointer reaches an id unknown at the start
    // (device names are often kept or reused, so a rearrangement may go unnoticed). That suffices:
    // levels live only during a drag, and its end gives them all back.
    private void MonitorChanged()
    {
        if (MonitorUnderPointer() is not { } monitor)
        {
            return;
        }

        if (!_monitorSetChanged && !_monitorIds.Contains(monitor))
        {
            // Levels hang on monitor ids: a new monitor set means give them back, once per drag.
            _monitorSetChanged = true;
            if (zoom.MonitorsChanged())
            {
                log.MonitorsChanged();
            }
        }

        // Without a held level this only keeps the report's monitor up to date.
        zoom.PointerOn(monitor);
    }

    private void Finish()
    {
        var activity = _activity;
        _activity = null;
        try
        {
            if (zoom.End() is { } report)
            {
                log.DragDone(report.Monitor, report.Kind, report.Action, report.LevelBefore, report.Level, report.CrossedMonitor, report.Cleared);
                activity?.SetTag("zoomin.monitor", report.Monitor)
                    .SetTag("zoomin.drag.kind", Name(report.Kind))
                    .SetTag("zoomin.drag.action", Name(report.Action))
                    .SetTag("zoomin.zoom.before", report.LevelBefore)
                    .SetTag("zoomin.zoom.during", report.Level)
                    .SetTag("zoomin.drag.crossed_monitor", report.CrossedMonitor)
                    .SetTag("zoomin.drag.cleared", report.Cleared);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Handled here, not passed on: a new drag right after must still start.
            log.DragFailed(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
        }
        finally
        {
            // Always stopped: an open activity would stay Activity.Current on the drag thread.
            activity?.Dispose();
        }
    }

    // MonitorAt gives an empty id when Windows cannot describe the monitor (e.g. mid display change).
    private string? MonitorUnderPointer() =>
        cursor.TryGetPosition(out var position) && monitors.MonitorAt(position) is { Length: > 0 } id ? id : null;

    private static string Name<T>(T value)
        where T : struct, Enum => value.ToString().ToLower(CultureInfo.InvariantCulture);
}
