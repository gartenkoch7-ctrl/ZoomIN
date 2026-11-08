using System.Diagnostics.Metrics;
using Vortice.Direct3D11;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// GPU time of one pass on one monitor as <c>zoomin.render.gpu_time</c>, the mean of 120 measured
/// frames (tags <c>zoomin.monitor</c>, <c>zoomin.pass</c>). Timestamp queries are read a few frames
/// later, so the render thread never waits for the GPU, and are only issued while someone listens
/// to the instrument. Tags and queries are built once; nothing here allocates (AGENTS.md 2.3).
/// Timed passes must not overlap: each has its own disjoint query.
/// </summary>
internal sealed class GpuPassTimer : IDisposable
{
    public const string PassTag = "zoomin.pass";

    private static readonly Histogram<double> s_gpuTime = Telemetry.Meter.CreateHistogram<double>(
        "zoomin.render.gpu_time", "ms", "Mean GPU time of one render pass over 120 frames");

    private readonly GpuStopwatch _stopwatch;
    private readonly KeyValuePair<string, object?> _monitor;
    private readonly KeyValuePair<string, object?> _pass;
    private SampleMean _mean;
    private bool _timing;

    /// <param name="pass">"magnify" (zoom and filter pass) or "cursor" (pointer layer, M15).</param>
    public GpuPassTimer(ID3D11Device device, string deviceName, string pass)
    {
        _stopwatch = new GpuStopwatch(device);
        _monitor = new(RenderMetrics.MonitorTag, deviceName);
        _pass = new(PassTag, pass);
    }

    /// <summary>Mean of the last full set of samples (for tests); 0 until then.</summary>
    public double LastMeanMilliseconds { get; private set; }

    /// <summary>Measured frames so far (for tests).</summary>
    public long Samples { get; private set; }

    /// <summary>Starts timing, but only while the instrument has a listener (diagnostics, OTLP).</summary>
    public void Begin(ID3D11DeviceContext context) => _timing = s_gpuTime.Enabled && _stopwatch.Begin(context);

    public void End(ID3D11DeviceContext context)
    {
        if (_timing)
        {
            _stopwatch.End(context);
            _timing = false;
        }
    }

    /// <summary>Takes finished measurements; call once per frame, outside Begin/End.</summary>
    public void Collect(ID3D11DeviceContext context)
    {
        while (_stopwatch.TryRead(context, out var milliseconds))
        {
            AddSample(milliseconds);
        }
    }

    /// <summary>Counts one measured frame; public for tests, which feed times without a GPU.</summary>
    public void AddSample(double milliseconds)
    {
        Samples++;
        if (_mean.Add(milliseconds, out var mean))
        {
            LastMeanMilliseconds = mean;
            s_gpuTime.Record(mean, _monitor, _pass);
        }
    }

    public void Dispose() => _stopwatch.Dispose();
}
