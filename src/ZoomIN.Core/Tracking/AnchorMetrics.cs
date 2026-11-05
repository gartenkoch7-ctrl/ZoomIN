using System.Diagnostics.Metrics;
using ZoomIN.Core.Diagnostics;

namespace ZoomIN.Core.Tracking;

/// <summary>
/// Counts when the view changes what it follows (mouse, focus, caret). Called from the render
/// thread, so the tags exist once and recording allocates nothing (ADR-0019).
/// </summary>
internal static class AnchorMetrics
{
    private static readonly Counter<long> s_switches = Telemetry.Meter.CreateCounter<long>(
        "zoomin.tracking.anchor_switches", "{switch}", "The view started following mouse, focus or caret");

    private static readonly KeyValuePair<string, object?> s_mouse = new("zoomin.anchor.source", "mouse");
    private static readonly KeyValuePair<string, object?> s_focus = new("zoomin.anchor.source", "focus");
    private static readonly KeyValuePair<string, object?> s_caret = new("zoomin.anchor.source", "caret");

    public static void Switched(AnchorSource to) => s_switches.Add(1, to switch
    {
        AnchorSource.Focus => s_focus,
        AnchorSource.Caret => s_caret,
        _ => s_mouse,
    });
}
