using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using SharpGen.Runtime;
using Vortice.Direct3D11;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// Keeps the smart inversion weights of one monitor up to date (ADR-0024): only while something
/// on it uses the smart filter, with a time smoothing that does not depend on the frame rate, and
/// with a fall-back to "keep hue" when the GPU cannot set it up or is too slow (decision 6).
/// <see cref="Prepare"/> runs every frame and must not allocate (AGENTS.md 2.3).
/// </summary>
internal sealed class SmartInvertLayer : IDisposable
{
    /// <summary>Budget of the ambient pass per frame (ADR-0024: ≤ 1 ms extra at 1440p on onboard graphics).</summary>
    public const double BudgetMilliseconds = 1.0;

    // Time constant of the smoothing: after 3 τ (~150 ms) a change is 95 % through.
    private const double SmoothingSeconds = 0.05;

    // Frames with the smart inversion vs all frames (zoomin.render.frame_interval) shows how often it runs.
    private static readonly Counter<long> s_frames = Telemetry.Meter.CreateCounter<long>(
        "zoomin.render.smart_invert_frames", "{frame}", "Frames that decided the smart inversion (ADR-0024)");

    private static readonly Histogram<double> s_gpuTime = Telemetry.Meter.CreateHistogram<double>(
        "zoomin.render.smart_invert_gpu_time", "ms", "Mean GPU time of the smart inversion ambient pass over 120 frames");

    private readonly AmbientPass? _pass;
    private readonly GpuStopwatch? _stopwatch;
    private readonly string _deviceName;
    private readonly SmartInvertHealth _health;
    private readonly ILogger _log;
    private readonly KeyValuePair<string, object?> _monitor;
    private long _lastUpdate;
    private bool _running;

    // Decided on a mean of SampleMean.Count frames, so a single slow frame changes nothing.
    private SampleMean _mean;

    public SmartInvertLayer(AmbientPass? pass, GpuStopwatch? stopwatch, string deviceName, SmartInvertHealth health, ILogger log)
    {
        _pass = pass;
        _stopwatch = stopwatch;
        _deviceName = deviceName;
        _health = health;
        _log = log;
        _monitor = new(RenderMetrics.MonitorTag, deviceName);
        if (pass is null)
        {
            FallBack("gpu_feature", 0);
        }
    }

    /// <summary>False after a fall-back: the smart filter then shows "keep hue" on this monitor.</summary>
    public bool Active { get; private set; } = true;

    /// <summary>Sets up the weights and the timing; without them the layer starts in fall-back.</summary>
    public static SmartInvertLayer Create(ID3D11Device device, int width, int height, string deviceName, SmartInvertHealth health, ILogger log)
    {
        AmbientPass? pass = null;
        try
        {
            pass = new AmbientPass(device, width, height);
            return new SmartInvertLayer(pass, new GpuStopwatch(device), deviceName, health, log);
        }
        catch (SharpGenException)
        {
            pass?.Dispose();
            return new SmartInvertLayer(null, null, deviceName, health, log);
        }
    }

    /// <summary>Share of the new weights after <paramref name="elapsedSeconds"/>; 1 when the layer was not running.</summary>
    public static float AlphaAfter(double elapsedSeconds, bool running) =>
        running ? (float)(1 - Math.Exp(-Math.Max(elapsedSeconds, 0) / SmoothingSeconds)) : 1f;

    /// <summary>
    /// Updates the weights from <paramref name="desktop"/> when <paramref name="needed"/>;
    /// returns them for the magnify pass, or null when nothing on the monitor is smart or after a fall-back.
    /// </summary>
    public ID3D11ShaderResourceView? Prepare(ID3D11DeviceContext context, ID3D11ShaderResourceView desktop, bool needed, bool protectPhotos)
    {
        if (!needed || !Active || _pass is null)
        {
            _running = false;
            return null;
        }

        var now = Stopwatch.GetTimestamp();
        var timed = _stopwatch?.Begin(context) == true;
        _pass.Update(context, desktop, AlphaAfter(Stopwatch.GetElapsedTime(_lastUpdate, now).TotalSeconds, _running), protectPhotos);
        if (timed)
        {
            _stopwatch!.End(context);
        }

        _lastUpdate = now;
        _running = true;
        s_frames.Add(1, _monitor);
        CheckBudget(context);

        // Even if this frame fell back: the palette of this frame still marks smart regions, so
        // they get this frame's weights; the next frame uploads the palette without them.
        return _pass.Weights;
    }

    /// <summary>Counts one measured frame; public for tests, which feed times without a GPU.</summary>
    public void AddSample(double milliseconds)
    {
        if (!_mean.Add(milliseconds, out var mean))
        {
            return;
        }

        s_gpuTime.Record(mean, _monitor);
        if (mean > BudgetMilliseconds)
        {
            FallBack("budget", mean);
        }
    }

    public void Dispose()
    {
        _stopwatch?.Dispose();
        _pass?.Dispose();
    }

    private void CheckBudget(ID3D11DeviceContext context)
    {
        if (_stopwatch is not null && _stopwatch.TryRead(context, out var milliseconds))
        {
            AddSample(milliseconds);
        }
    }

    // Once per pipeline: a log line, never one per frame (AGENTS.md 2.5).
    private void FallBack(string reason, double milliseconds)
    {
        Active = false;
        _running = false;
        _log.SmartInvertFallback(_deviceName, reason, milliseconds);
        _health.Report(new SmartInvertFallback(_deviceName, reason, milliseconds));
    }
}
