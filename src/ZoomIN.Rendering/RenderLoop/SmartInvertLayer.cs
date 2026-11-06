using System.Diagnostics;
using System.Diagnostics.Metrics;
using Vortice.Direct3D11;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// Keeps the smart inversion weights of one monitor up to date (ADR-0024): only while something
/// on it uses the smart filter, and with a time smoothing that does not depend on the frame rate.
/// <see cref="Prepare"/> runs every frame and must not allocate (AGENTS.md 2.3).
/// </summary>
internal sealed class SmartInvertLayer(AmbientPass pass, string deviceName)
{
    // Time constant of the smoothing: after 3 τ (~150 ms) a change is 95 % through.
    private const double SmoothingSeconds = 0.05;

    // Frames with the smart inversion vs all frames (zoomin.render.frame_interval) shows how often it runs.
    private static readonly Counter<long> s_frames = Telemetry.Meter.CreateCounter<long>(
        "zoomin.render.smart_invert_frames", "{frame}", "Frames that decided the smart inversion (ADR-0024)");

    private readonly KeyValuePair<string, object?> _monitor = new(RenderMetrics.MonitorTag, deviceName);
    private long _lastUpdate;
    private bool _running;

    /// <summary>Share of the new weights after <paramref name="elapsedSeconds"/>; 1 when the layer was not running.</summary>
    public static float AlphaAfter(double elapsedSeconds, bool running) =>
        running ? (float)(1 - Math.Exp(-Math.Max(elapsedSeconds, 0) / SmoothingSeconds)) : 1f;

    /// <summary>
    /// Updates the weights from <paramref name="desktop"/> when <paramref name="needed"/>;
    /// returns them for the magnify pass, or null when nothing on the monitor is smart.
    /// </summary>
    public ID3D11ShaderResourceView? Prepare(ID3D11DeviceContext context, ID3D11ShaderResourceView desktop, bool needed, bool protectPhotos)
    {
        if (!needed)
        {
            _running = false;
            return null;
        }

        var now = Stopwatch.GetTimestamp();
        pass.Update(context, desktop, AlphaAfter(Stopwatch.GetElapsedTime(_lastUpdate, now).TotalSeconds, _running), protectPhotos);
        _lastUpdate = now;
        _running = true;
        s_frames.Add(1, _monitor);
        return pass.Weights;
    }
}
