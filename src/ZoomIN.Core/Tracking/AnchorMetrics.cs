using System.Diagnostics.Metrics;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Modes;

namespace ZoomIN.Core.Tracking;

/// <summary>
/// Counts when the view changes what it follows (mouse, focus, caret) and how often the mouse
/// moves the view. Called from the render thread, so the tags exist once and recording allocates
/// nothing (ADR-0019).
/// </summary>
internal static class AnchorMetrics
{
    private static readonly Counter<long> s_switches = Telemetry.Meter.CreateCounter<long>(
        "zoomin.tracking.anchor_switches", "{switch}", "The view started following mouse, focus or caret");

    // Frames, not gestures: the rate per second says how restless the view is in each mode (M17 A1).
    private static readonly Counter<long> s_mousePushes = Telemetry.Meter.CreateCounter<long>(
        "zoomin.tracking.mouse_push", "{frame}", "Frames in which a mouse movement moved the view");

    private static readonly KeyValuePair<string, object?> s_mouse = new("zoomin.anchor.source", "mouse");
    private static readonly KeyValuePair<string, object?> s_focus = new("zoomin.anchor.source", "focus");
    private static readonly KeyValuePair<string, object?> s_caret = new("zoomin.anchor.source", "caret");

    private static readonly KeyValuePair<string, object?> s_center = new("zoomin.mouse.follow", "center");
    private static readonly KeyValuePair<string, object?> s_margin = new("zoomin.mouse.follow", "margin");
    private static readonly KeyValuePair<string, object?> s_fixpoint = new("zoomin.mouse.follow", "fixpoint");

    public static void Switched(AnchorSource to) => s_switches.Add(1, to switch
    {
        AnchorSource.Focus => s_focus,
        AnchorSource.Caret => s_caret,
        _ => s_mouse,
    });

    /// <summary>
    /// Counts a frame in which a mouse step moved the view. Not docked (always the area around the
    /// mouse), not at 1× (nothing visible moves), not while the zoom changes (re-anchoring).
    /// </summary>
    public static void MouseStep(in AnchorInput input, bool moved, double previousZoom)
    {
        if (moved && input.Mode != ZoomMode.Docked && input.Zoom > 1 && input.Zoom == previousZoom)
        {
            MousePushed(input.Options.MouseFollow);
        }
    }

    public static void MousePushed(MouseFollowMode mode) => s_mousePushes.Add(1, mode switch
    {
        MouseFollowMode.Center => s_center,
        MouseFollowMode.Fixpoint => s_fixpoint,
        _ => s_margin,
    });
}
