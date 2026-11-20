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
/// with a fall-back to "keep hue" only when the GPU cannot set it up (decision 6). Being slow is
/// measured and reported, but never switches it off (ADR-0032).
/// <see cref="Prepare"/> runs every frame and must not allocate (AGENTS.md 2.3).
/// </summary>
internal sealed class SmartInvertLayer : IDisposable
{
    // Time constant of the smoothing: after 3 τ (~150 ms) a change is 95 % through.
    private const double SmoothingSeconds = 0.05;

    // After 6 τ less than 0.3 % of the old weights is left: the weights are final until the next change.
    private const double SettleSeconds = 6 * SmoothingSeconds;

    // One frame at 60 Hz: the step the smoothing takes for the first change after a settled pause.
    private const double OneFrameSeconds = 1.0 / 60;

    // Frames with the smart inversion vs all frames (zoomin.render.frame_interval) shows how often it runs.
    private static readonly Counter<long> s_frames = Telemetry.Meter.CreateCounter<long>(
        "zoomin.render.smart_invert_frames", "{frame}", "Frames that decided the smart inversion (ADR-0024)");

    private static readonly Histogram<double> s_gpuTime = Telemetry.Meter.CreateHistogram<double>(
        "zoomin.render.smart_invert_gpu_time", "ms", "Mean extra GPU time of the smart inversion: ambient pass over 120 frames plus magnify extra");

    private readonly AmbientPass? _pass;
    private readonly GpuStopwatch? _stopwatch;
    private readonly string _deviceName;
    private readonly SmartInvertHealth _health;
    private readonly ILogger _log;
    private readonly KeyValuePair<string, object?> _monitor;
    private long _lastUpdate;
    private bool _running;
    private long _changedAt;
    private double _ambientMean;
    private double _magnifyExtra;
    private bool _settled;
    private SmartBudget _budget;
    private bool _probeRetried;

    // Decided on a mean of SampleMean.Count frames, so a single slow frame changes nothing.
    private SampleMean _mean;

    /// <param name="refreshHz">The monitor's refresh rate; the budget is a share of its frame time.</param>
    public SmartInvertLayer(
        AmbientPass? pass, GpuStopwatch? stopwatch, string deviceName, SmartInvertHealth health, ILogger log, double refreshHz = 60)
    {
        _budget = new SmartBudget(refreshHz);
        _pass = pass;
        _stopwatch = stopwatch;
        _deviceName = deviceName;
        _health = health;
        _log = log;
        _monitor = new(RenderMetrics.MonitorTag, deviceName);
        if (pass is null)
        {
            FallBack(SmartInvertFallback.GpuFeature);
        }
    }

    /// <summary>Extra GPU time per frame the smart inversion may take on this monitor (<see cref="SmartBudget"/>).</summary>
    public double BudgetMilliseconds => _budget.Milliseconds;

    /// <summary>False after a fall-back: the smart filter then shows "keep hue" on this monitor.</summary>
    public bool Active { get; private set; } = true;

    /// <summary>The smoothing still moves the weights: the monitor keeps drawing frames (M14 step 3).</summary>
    public bool Settling { get; private set; }

    /// <summary>The cost probe delivered the magnify extra at least once (for tests).</summary>
    public bool MagnifyExtraReported { get; private set; }

    /// <summary>Sets up the weights and the timing; without them the layer starts in fall-back.</summary>
    public static SmartInvertLayer Create(
        ID3D11Device device, int width, int height, string deviceName, SmartInvertHealth health, ILogger log, double refreshHz = 60)
    {
        AmbientPass? pass = null;
        try
        {
            pass = new AmbientPass(device, width, height);
            return new SmartInvertLayer(pass, new GpuStopwatch(device), deviceName, health, log, refreshHz);
        }
        catch (SharpGenException)
        {
            pass?.Dispose();
            return new SmartInvertLayer(null, null, deviceName, health, log, refreshHz);
        }
    }

    /// <summary>Share of the new weights after <paramref name="elapsedSeconds"/>; 1 when the layer was not running.</summary>
    public static float AlphaAfter(double elapsedSeconds, bool running) =>
        running ? (float)(1 - Math.Exp(-Math.Max(elapsedSeconds, 0) / SmoothingSeconds)) : 1f;

    /// <summary>
    /// Updates the weights from <paramref name="desktop"/> when <paramref name="needed"/>;
    /// returns them for the magnify pass, or null when nothing on the monitor is smart or after a fall-back.
    /// </summary>
    /// <param name="inputChanged">
    /// A new desktop image or other options came in. Without one the weights stop changing once the
    /// smoothing has settled, and the ambient pass is skipped (M14 step 3).
    /// </param>
    public ID3D11ShaderResourceView? Prepare(
        ID3D11DeviceContext context, ID3D11ShaderResourceView desktop, bool needed, bool protectPhotos, bool inputChanged = true)
    {
        if (!needed || !Active || _pass is null)
        {
            _running = false;
            Settling = false;
            return null;
        }

        var now = Stopwatch.GetTimestamp();
        if (inputChanged || !_running)
        {
            _changedAt = now;
        }

        Settling = Stopwatch.GetElapsedTime(_changedAt, now).TotalSeconds < SettleSeconds;
        if (_running && !Settling)
        {
            _settled = true;
            CheckBudget(context);
            return _pass.Weights;
        }

        // After a settled pause (no updates, maybe idle frames) the time since the last update says
        // nothing about the change: it starts now. Smooth it like one frame, never jump at once.
        var elapsed = Stopwatch.GetElapsedTime(_lastUpdate, now).TotalSeconds;
        if (_settled)
        {
            elapsed = Math.Min(elapsed, OneFrameSeconds);
            _settled = false;
        }

        var timed = _stopwatch?.Begin(context) == true;
        _pass.Update(context, desktop, AlphaAfter(elapsed, _running), protectPhotos);
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

        _ambientMean = mean;
        s_gpuTime.Record(mean + _magnifyExtra, _monitor);
        CheckTotal();
    }

    /// <summary>
    /// What the smart inversion adds to the magnify pass (PassCostProbe, M14 step 5). Before, the
    /// budget saw only the ambient pass, although the edge-aware weight in Magnify.hlsl costs as much.
    /// </summary>
    public void SetMagnifyExtra(double milliseconds)
    {
        // Counts from the next mean on; if it alone breaks the budget it is measured once more (CheckTotal).
        MagnifyExtraReported = true;
        _magnifyExtra = milliseconds;
    }

    /// <summary>The magnify extra should be measured again (DesktopLayer restarts its probe once).</summary>
    public bool ProbeWanted { get; private set; }

    /// <summary>DesktopLayer took <see cref="ProbeWanted"/> and restarted its probe.</summary>
    public void ProbeRestarted() => ProbeWanted = false;

    /// <summary>The smart inversion took more than its budget on this monitor; it still runs (ADR-0032).</summary>
    public bool OverBudget { get; private set; }

    // The budget covers everything the smart inversion adds: ambient pass plus magnify extra. The
    // extra is one measurement per pipeline, so when it alone pushes the total over, it is measured
    // once more before reporting (it may have run at idle clocks).
    // Over budget only reports: falling back to "keep hue" inverted the whole screen, dark windows
    // too, for the rest of the pipeline – far worse than a few slower frames (ADR-0032).
    private void CheckTotal()
    {
        var total = _ambientMean + _magnifyExtra;
        if (!_budget.Exceeded(total) || !Active || OverBudget)
        {
            return;
        }

        if (_ambientMean <= BudgetMilliseconds && !_probeRetried)
        {
            _probeRetried = true;
            _magnifyExtra = 0;
            ProbeWanted = true;
            _budget.Reset();
            return;
        }

        // Once per pipeline: a log line, never one per frame (AGENTS.md 2.5).
        OverBudget = true;
        _log.SmartInvertOverBudget(_deviceName, total, BudgetMilliseconds);
        _health.Report(new SmartInvertFallback(_deviceName, SmartInvertFallback.Slow, total));
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
    private void FallBack(string reason)
    {
        Active = false;
        _running = false;

        // One more frame shows the fall-back even when nothing else changes (M14 step 3).
        Settling = true;
        _log.SmartInvertFallback(_deviceName, reason, 0);
        _health.Report(new SmartInvertFallback(_deviceName, reason, 0));
    }
}
