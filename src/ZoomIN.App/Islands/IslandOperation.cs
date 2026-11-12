using System.Diagnostics;
using ZoomIN.Core.Diagnostics;

namespace ZoomIN.App.Islands;

/// <summary>
/// The operations of the islands (M19 B1): <c>zoomin.islands.edit</c> from the shortcut until the
/// edit mode ends (on another thread, so it never stays <see cref="Activity.Current"/>), and
/// <c>zoomin.islands.toggle</c> for Ctrl+Alt+I. Only counts, levels and reasons as tags.
/// </summary>
internal static class IslandOperation
{
    public static Activity? StartEdit(int count)
    {
        // A root of its own that does not become the parent of what runs next on this thread (as the overview).
        var previous = Activity.Current;
        Activity.Current = null;
        var operation = Telemetry.Source.StartActivity("zoomin.islands.edit", ActivityKind.Internal)?
            .SetTag("zoomin.islands.before", count);
        Activity.Current = previous;
        return operation;
    }

    public static void StopEdit(Activity? operation, string reason, int count)
    {
        if (operation is null)
        {
            return;
        }

        operation.SetTag("zoomin.islands.after", count);
        operation.SetTag("zoomin.islands.end", reason);
        var current = Activity.Current;
        operation.Stop();
        Activity.Current = ReferenceEquals(current, operation) ? null : current;
    }

    public static void Toggled(bool visible, int count)
    {
        using var operation = Telemetry.Source.StartActivity("zoomin.islands.toggle");
        operation?.SetTag("zoomin.islands.visible", visible);
        operation?.SetTag("zoomin.islands.count", count);
    }
}
