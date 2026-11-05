using System.Diagnostics.Metrics;
using ZoomIN.Core.Diagnostics;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// Measurements of one monitor's render thread (ADR-0019). The instruments exist once per process
/// and the monitor tag is built once per renderer, so recording in the render loop allocates
/// nothing (checked by RenderMetricsAllocationTests with the OpenTelemetry SDK listening).
/// Without a listener (default level "Normal") every call is a cheap no-op.
/// </summary>
internal sealed class RenderMetrics
{
    public const string MonitorTag = "zoomin.monitor";
    public const string ReasonTag = "zoomin.reason";

    private static readonly Histogram<double> s_frameInterval = Telemetry.Meter.CreateHistogram<double>(
        "zoomin.render.frame_interval", "ms", "Time between two presented frames; spikes are stutters");

    private static readonly Counter<long> s_framesMissed = Telemetry.Meter.CreateCounter<long>(
        "zoomin.render.frames_missed", "{frame}", "Frames the compositor did not ask for in time");

    private static readonly Histogram<double> s_showLatency = Telemetry.Meter.CreateHistogram<double>(
        "zoomin.render.show_latency", "ms", "From waking up (zoom or filter on) to the first visible frame");

    private static readonly Counter<long> s_restarts = Telemetry.Meter.CreateCounter<long>(
        "zoomin.render.pipeline_restarts", "{restart}", "Render pipeline rebuilt after a failure or display change");

    private readonly KeyValuePair<string, object?> _monitor;

    public RenderMetrics(string deviceName)
    {
        _monitor = new(MonitorTag, deviceName);
    }

    /// <summary>Render loop: a frame was presented <paramref name="intervalMs"/> after the previous one.</summary>
    public void FramePresented(double intervalMs) => s_frameInterval.Record(intervalMs, _monitor);

    /// <summary>Render loop: the compositor did not request a frame in time.</summary>
    public void FrameMissed() => s_framesMissed.Add(1, _monitor);

    public void Shown(double latencyMs) => s_showLatency.Record(latencyMs, _monitor);

    /// <param name="reason">"failure" or "display_changed".</param>
    public void Restarted(string reason) => s_restarts.Add(1, _monitor, new KeyValuePair<string, object?>(ReasonTag, reason));
}
