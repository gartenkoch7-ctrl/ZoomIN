using System.Diagnostics;
using System.Diagnostics.Metrics;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Monitors;

namespace ZoomIN.App.Freeze;

/// <summary>
/// Telemetry of frozen monitors (G2): the operation <c>zoomin.view.freeze</c> from freezing until
/// it is lifted (started and stopped on different threads, so it never stays
/// <see cref="Activity.Current"/>), and the gauge <c>zoomin.view.frozen_monitors</c>.
/// </summary>
internal static class FreezeOperation
{
    private static readonly UpDownCounter<long> s_frozen = Telemetry.Meter.CreateUpDownCounter<long>(
        "zoomin.view.frozen_monitors", unit: "{monitor}", description: "Monitors frozen right now (G2)");

    public static Activity? Start(FrozenMonitor monitor)
    {
        s_frozen.Add(1);

        // A root of its own, as the overview's: not a child of a KI-Debug command that pressed the
        // shortcut, and not the parent of what runs next on the hotkey thread.
        var previous = Activity.Current;
        Activity.Current = null;
        var operation = Telemetry.Source.StartActivity("zoomin.view.freeze", ActivityKind.Internal)?
            .SetTag("zoomin.monitor", monitor.Id)
            .SetTag("zoomin.monitor.number", monitor.Number)
            .SetTag("zoomin.zoom.level", Math.Round(monitor.Level, 2))
            .SetTag("zoomin.mode", monitor.Mode.ToString())
            .SetTag("zoomin.freeze.image", monitor.ImageKind);
        Activity.Current = previous;
        return operation;
    }

    /// <param name="reason">Why it ended: <c>command</c>, <c>display_change</c>, <c>desktop_switch</c>, <c>session_unlock</c>, <c>move_refused</c>, <c>shortcut_gone</c>, <c>pointer_inside</c>, <c>failure</c>, <c>exit</c>.</param>
    public static void Stop(Activity? operation, string reason)
    {
        s_frozen.Add(-1);
        if (operation is null)
        {
            return;
        }

        operation.SetTag("zoomin.freeze.end", reason);
        var current = Activity.Current;
        operation.Stop();
        Activity.Current = ReferenceEquals(current, operation) ? null : current;
    }
}
