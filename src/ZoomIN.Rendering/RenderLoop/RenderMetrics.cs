using System.Diagnostics;
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
    public const string StateTag = "zoomin.state";

    private static readonly Histogram<double> s_frameInterval = Telemetry.Meter.CreateHistogram<double>(
        "zoomin.render.frame_interval", "ms", "Time between two presented frames; spikes are stutters");

    private static readonly Counter<long> s_framesMissed = Telemetry.Meter.CreateCounter<long>(
        "zoomin.render.frames_missed", "{frame}", "Frames the compositor did not ask for in time");

    private static readonly Histogram<double> s_showLatency = Telemetry.Meter.CreateHistogram<double>(
        "zoomin.render.show_latency", "ms", "From waking up (zoom or filter on) to the first visible frame");

    private static readonly Counter<long> s_restarts = Telemetry.Meter.CreateCounter<long>(
        "zoomin.render.pipeline_restarts", "{restart}", "Render pipeline rebuilt after a failure or display change");

    // Active vs paused time gives the pause share of the filter per window (ADR-0020).
    private static readonly Counter<double> s_overlayTime = Telemetry.Meter.CreateCounter<double>(
        "zoomin.render.overlay_time", "s", "Time the render thread worked (active) or slept (paused)");

    // Startup measurement (M14): how long after the process start each monitor could zoom.
    private static readonly Histogram<double> s_readyTime = Telemetry.Meter.CreateHistogram<double>(
        "zoomin.render.ready_time", "ms", "From process start to the first ready zoom pipeline of a monitor");

    // Rebuilds later in the session are not startup; only the first pipeline per monitor counts,
    // and only within the first minute: a monitor plugged in hours later is not a slow start.
    private static readonly TimeSpan s_startupWindow = TimeSpan.FromMinutes(1);
    private static readonly HashSet<string> s_readyMonitors = new(StringComparer.Ordinal);
    private static readonly Lock s_readyGate = new();

    private readonly KeyValuePair<string, object?> _monitor;
    private readonly KeyValuePair<string, object?> _active = new(StateTag, "active");
    private readonly KeyValuePair<string, object?> _paused = new(StateTag, "paused");

    public RenderMetrics(string deviceName)
    {
        _monitor = new(MonitorTag, deviceName);
    }

    /// <summary>Render loop: a frame was presented <paramref name="intervalMs"/> after the previous one.</summary>
    public void FramePresented(double intervalMs) => s_frameInterval.Record(intervalMs, _monitor);

    /// <summary>Render loop: the compositor did not request a frame in time.</summary>
    public void FrameMissed() => s_framesMissed.Add(1, _monitor);

    public void Shown(double latencyMs) => s_showLatency.Record(latencyMs, _monitor);

    /// <summary>A pipeline is ready (not per frame); records the time since process start once per monitor.</summary>
    public void PipelineReady()
    {
        lock (s_readyGate)
        {
            if (!s_readyMonitors.Add((string)_monitor.Value!))
            {
                return;
            }
        }

        using var process = Process.GetCurrentProcess();
        var sinceStart = DateTime.Now - process.StartTime;
        if (sinceStart < s_startupWindow)
        {
            s_readyTime.Record(sinceStart.TotalMilliseconds, _monitor);
        }
    }

    /// <summary>The thread worked for <paramref name="seconds"/> before going to sleep.</summary>
    public void Active(double seconds) => s_overlayTime.Add(seconds, _monitor, _active);

    /// <summary>The thread slept for <paramref name="seconds"/> (nothing to zoom or filter).</summary>
    public void Paused(double seconds) => s_overlayTime.Add(seconds, _monitor, _paused);

    /// <param name="reason">"failure" or "display_changed".</param>
    public void Restarted(string reason) => s_restarts.Add(1, _monitor, new KeyValuePair<string, object?>(ReasonTag, reason));
}
