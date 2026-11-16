using System.Diagnostics;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Overview;

namespace ZoomIN.App.Overview;

/// <summary>
/// The operation <c>zoomin.view.overview</c> (B2): from the shortcut until zooming in, Esc or a
/// cancel. It starts and stops on different threads, so it never stays <see cref="Activity.Current"/>.
/// </summary>
internal static class OverviewOperation
{
    public static Activity? Start(OverviewSession session)
    {
        // A root of its own (a default parent context would fall back to Activity.Current, e.g. a
        // KI-Debug command), and it must not become the parent of whatever runs next on this
        // thread, so Activity.Current is cleared first and put back right away.
        var previous = Activity.Current;
        Activity.Current = null;
        var operation = Telemetry.Source.StartActivity("zoomin.view.overview", ActivityKind.Internal)?
            .SetTag("zoomin.monitor", session.MonitorId)
            .SetTag("zoomin.overview.level_before", Math.Round(session.LevelBefore, 2))
            .SetTag("zoomin.mode", session.Mode.ToString());
        Activity.Current = previous;
        return operation;
    }

    public static void Stop(Activity? operation, OverviewEnded ended)
    {
        if (operation is null)
        {
            return;
        }

        operation.SetTag("zoomin.overview.kind", ended.Held ? "hold" : "tap");
        operation.SetTag("zoomin.overview.end", ended.End switch
        {
            OverviewEnd.ZoomIn => "zoom_in",
            OverviewEnd.Escape => "escape",
            _ => "cancelled",
        });

        // Stop makes the parent (none) current on this thread; keep what was current here.
        var current = Activity.Current;
        operation.Stop();
        Activity.Current = ReferenceEquals(current, operation) ? null : current;
    }
}
