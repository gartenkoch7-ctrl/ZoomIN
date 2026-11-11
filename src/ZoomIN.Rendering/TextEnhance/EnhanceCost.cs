using System.Diagnostics.Metrics;
using Vortice.Direct3D11;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.Rendering.TextEnhance;

/// <summary>
/// What "Schärfe"/"Fett" add to the magnify pass on one monitor (M18, D1), as
/// <c>zoomin.render.enhance_gpu_ms</c>: after each change of the values the desktop is drawn with
/// and without them for <see cref="PassCostProbe.Frames"/> frames (<see cref="PassCostProbe"/>).
/// Only while someone listens to the instrument, so a normal session draws nothing extra. The
/// instrument and the tag exist from the start; nothing here allocates (AGENTS.md 2.3).
/// </summary>
internal sealed class EnhanceCost : IDisposable
{
    private static readonly Histogram<double> s_extra = Telemetry.Meter.CreateHistogram<double>(
        "zoomin.render.enhance_gpu_ms", "ms", "Extra GPU time of sharpness and bold in the magnify pass (M18, D1)");

    private readonly KeyValuePair<string, object?> _monitor;
    private TextEnhancement _measuring;

    public EnhanceCost(ID3D11Device device, string deviceName)
    {
        Probe = new PassCostProbe(device);
        _monitor = new(RenderMetrics.MonitorTag, deviceName);
    }

    /// <summary>Times the draws: base = without, variant = with "Schärfe"/"Fett".</summary>
    public PassCostProbe Probe { get; }

    /// <summary>Last measured extra (for tests); 0 until then.</summary>
    public double LastExtraMilliseconds { get; private set; }

    /// <summary>Measurements reported so far (for tests).</summary>
    public int Reports { get; private set; }

    /// <summary>
    /// One frame with <paramref name="enhance"/> on: takes finished results and returns whether
    /// this frame should draw the probe pair. New values measure again.
    /// </summary>
    public bool Tick(ID3D11DeviceContext context, TextEnhancement enhance)
    {
        if (Probe.Collect(context))
        {
            LastExtraMilliseconds = Probe.ExtraMilliseconds;
            Reports++;
            s_extra.Record(LastExtraMilliseconds, _monitor);
        }

        if (!s_extra.Enabled)
        {
            return false;
        }

        if (enhance != _measuring)
        {
            _measuring = enhance;
            Probe.Restart();
        }

        Probe.Tick();
        return Probe.Wanted;
    }

    public void Dispose() => Probe.Dispose();
}
