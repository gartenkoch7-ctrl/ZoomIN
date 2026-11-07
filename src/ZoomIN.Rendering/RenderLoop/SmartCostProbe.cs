using Vortice.Direct3D11;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// Measures what the smart inversion adds to the magnify pass (M14 step 5): for <see cref="Frames"/>
/// frames the desktop is drawn plain and smart one after the other, each timed on the GPU. The
/// ambient pass alone was all the budget saw before; the edge-aware weight in Magnify.hlsl costs as
/// much again. Both draws go to the frame's own target, which the real draw overwrites afterwards,
/// so nothing of it is seen. No allocations (AGENTS.md 2.3).
/// </summary>
internal sealed class SmartCostProbe : IDisposable
{
    /// <summary>Probe frames per measurement: enough for a stable mean, short enough to be free.</summary>
    public const int Frames = 32;

    /// <summary>Smart frames before measuring: right after the start the GPU may still run at low clocks.</summary>
    public const int WarmupFrames = 60;

    // Frames to wait for the queries after the last probe draw before measuring once more.
    private const int CollectFrames = 30;

    private readonly GpuStopwatch _plain;
    private readonly GpuStopwatch _smart;
    private int _warmup;
    private int _drawn;
    private int _waited;
    private bool _retried;
    private bool _reported;
    private int _plainCount;
    private int _smartCount;
    private double _plainSum;
    private double _smartSum;
    private bool _plainTimed;
    private bool _smartTimed;

    public SmartCostProbe(ID3D11Device device)
    {
        _plain = new GpuStopwatch(device);
        _smart = new GpuStopwatch(device);
    }

    /// <summary>Probe draws are wanted in this frame.</summary>
    public bool Wanted => _warmup >= WarmupFrames && _drawn < Frames;

    /// <summary>
    /// The probe frames are drawn and enough came back; <see cref="ExtraMilliseconds"/> holds the
    /// result. Half is enough: a disjoint timestamp (GPU clock change) drops a measurement.
    /// </summary>
    public bool Done => _drawn >= Frames && _plainCount >= Frames / 2 && _smartCount >= Frames / 2;

    /// <summary>Mean extra GPU time of the smart magnify pass over the plain one, never negative.</summary>
    public double ExtraMilliseconds => Done ? Math.Max(0, (_smartSum / _smartCount) - (_plainSum / _plainCount)) : 0;

    /// <summary>Measures again, e.g. when the whole monitor turned smart (a dearer case than a few windows).</summary>
    public void Restart()
    {
        _warmup = 0;
        _drawn = 0;
        _reported = false;
        _waited = 0;
        _plainCount = 0;
        _smartCount = 0;
        _plainSum = 0;
        _smartSum = 0;
    }

    /// <summary>One frame with smart weights; call before <see cref="Wanted"/>.</summary>
    public void Tick()
    {
        if (_warmup < WarmupFrames)
        {
            _warmup++;
        }
    }

    public void BeginPlain(ID3D11DeviceContext context) => _plainTimed = _plain.Begin(context);

    public void EndPlain(ID3D11DeviceContext context)
    {
        if (_plainTimed)
        {
            _plain.End(context);
        }
    }

    public void BeginSmart(ID3D11DeviceContext context) => _smartTimed = _smart.Begin(context);

    public void EndSmart(ID3D11DeviceContext context)
    {
        if (_smartTimed)
        {
            _smart.End(context);
        }

        // A frame counts once both draws were timed; a set still pending on the GPU repeats it.
        if (_plainTimed && _smartTimed)
        {
            _drawn++;
        }
    }

    /// <summary>Takes finished measurements; true once per measurement, when its result is ready.</summary>
    public bool Collect(ID3D11DeviceContext context)
    {
        // During the warm-up only results of an earlier measurement can come back (before a
        // Restart): they free their query sets but do not count.
        var counting = _warmup >= WarmupFrames;
        while (_plain.TryRead(context, out var plain))
        {
            if (counting)
            {
                _plainSum += plain;
                _plainCount++;
            }
        }

        while (_smart.TryRead(context, out var smart))
        {
            if (counting)
            {
                _smartSum += smart;
                _smartCount++;
            }
        }

        // Too many disjoint results (clock changes): measure once more, then give up quietly
        // (the budget keeps checking the ambient pass).
        if (_drawn >= Frames && !Done && ++_waited > CollectFrames && !_retried)
        {
            _retried = true;
            Restart();
        }

        // Done can already be true when the last probe frame was drawn (EndSmart), so "became done
        // in this call" would miss it: report each result exactly once instead.
        if (!Done || _reported)
        {
            return false;
        }

        _reported = true;
        return true;
    }

    public void Dispose()
    {
        _smart.Dispose();
        _plain.Dispose();
    }
}
