using System.Diagnostics.Metrics;
using ZoomIN.Core.Diagnostics;

namespace ZoomIN.Core.Zoom;

/// <summary>
/// Counts changes of temporary zoom levels (M17), so a monitor stuck at 1× can be traced to an
/// owner that set a level and never released it. Tags exist once; recording allocates nothing.
/// </summary>
internal static class TemporaryZoomMetrics
{
    private static readonly Counter<long> s_changes = Telemetry.Meter.CreateCounter<long>(
        "zoomin.zoom.temporary_levels", "{change}", "A temporary zoom level was set, released, moved or cleared");

    private static readonly KeyValuePair<string, object?> s_overview = new("zoomin.owner", "overview");
    private static readonly KeyValuePair<string, object?> s_windowDrag = new("zoomin.owner", "window_drag");
    private static readonly KeyValuePair<string, object?> s_islandEdit = new("zoomin.owner", "island_edit");
    private static readonly KeyValuePair<string, object?> s_set = new("zoomin.action", "set");
    private static readonly KeyValuePair<string, object?> s_release = new("zoomin.action", "release");
    private static readonly KeyValuePair<string, object?> s_move = new("zoomin.action", "move");
    private static readonly KeyValuePair<string, object?> s_cleared = new("zoomin.action", "cleared");

    public static void Set(TemporaryZoomOwner owner) => s_changes.Add(1, Owner(owner), s_set);

    public static void Released(TemporaryZoomOwner owner, int count) => s_changes.Add(count, Owner(owner), s_release);

    public static void Moved(TemporaryZoomOwner owner) => s_changes.Add(1, Owner(owner), s_move);

    /// <summary>A user command (on/off, mode, step from or to 1×) dropped the owner's level.</summary>
    public static void Cleared(TemporaryZoomOwner owner) => s_changes.Add(1, Owner(owner), s_cleared);

    // Exhaustive on purpose: a new owner must get its own tag, or a stuck level hides behind another.
    private static KeyValuePair<string, object?> Owner(TemporaryZoomOwner owner) => owner switch
    {
        TemporaryZoomOwner.Overview => s_overview,
        TemporaryZoomOwner.WindowDrag => s_windowDrag,
        TemporaryZoomOwner.IslandEdit => s_islandEdit,
        _ => throw new ArgumentOutOfRangeException(nameof(owner), owner, "Every owner needs its own metric tag."),
    };
}
