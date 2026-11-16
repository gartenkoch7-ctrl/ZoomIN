using System.Diagnostics.Metrics;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Tracking;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// Measurements of the highlights (E2): which caret shape people actually see. The tags are built
/// once, so recording in the render loop allocates nothing (ADR-0019).
/// </summary>
internal sealed class HighlightMetrics
{
    public const string CaretShapeTag = "zoomin.highlight.caret_shape";

    private static readonly Counter<long> s_caretFrames = Telemetry.Meter.CreateCounter<long>(
        "zoomin.highlight.caret_frames", "{frame}", "Drawn frames with a caret marker, by shape");

    private static readonly KeyValuePair<string, object?> s_bar = new(CaretShapeTag, "bar");
    private static readonly KeyValuePair<string, object?> s_wedges = new(CaretShapeTag, "wedges");
    private static readonly KeyValuePair<string, object?> s_frame = new(CaretShapeTag, "frame");
    private static readonly KeyValuePair<string, object?> s_circle = new(CaretShapeTag, "circle");

    private readonly KeyValuePair<string, object?> _monitor;

    public HighlightMetrics(string deviceName)
    {
        _monitor = new(RenderMetrics.MonitorTag, deviceName);
    }

    /// <summary>Render loop: a frame with a caret marker of <paramref name="shape"/> was drawn.</summary>
    public void CaretDrawn(CaretMarkerShape shape) => s_caretFrames.Add(1, _monitor, shape switch
    {
        CaretMarkerShape.Wedges => s_wedges,
        CaretMarkerShape.Frame => s_frame,
        CaretMarkerShape.Circle => s_circle,

        // The geometry draws every other value as a bar.
        _ => s_bar,
    });
}
