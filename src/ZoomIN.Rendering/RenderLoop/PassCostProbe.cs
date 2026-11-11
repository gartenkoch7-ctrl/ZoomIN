using Vortice.Direct3D11;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// Measures what a variant adds to the magnify pass: for <see cref="Frames"/> frames the desktop is
/// drawn as base and as variant one after the other, each timed on the GPU. Used for the smart
/// inversion (M14 step 5: the edge-aware weight in Magnify.hlsl costs as much as the ambient pass)
/// and for "Schärfe"/"Fett" (M18, D1). Both draws go to the frame's own target, which the real draw
/// overwrites afterwards, so nothing of it is seen. No allocations (AGENTS.md 2.3).
/// </summary>
internal sealed class PassCostProbe : IDisposable
{
    /// <summary>Probe frames per measurement: enough for a stable mean, short enough to be free.</summary>
    public const int Frames = 32;

    /// <summary>Frames before measuring: right after the start the GPU may still run at low clocks.</summary>
    public const int WarmupFrames = 60;

    // Frames to wait for the queries after the last probe draw before measuring once more.
    private const int CollectFrames = 30;

    private readonly GpuStopwatch _base;
    private readonly GpuStopwatch _variant;
    private int _warmup;
    private int _drawn;
    private int _waited;
    private bool _retried;
    private bool _reported;
    private int _baseCount;
    private int _variantCount;
    private double _baseSum;
    private double _variantSum;
    private bool _baseTimed;
    private bool _variantTimed;

    public PassCostProbe(ID3D11Device device)
    {
        _base = new GpuStopwatch(device);
        _variant = new GpuStopwatch(device);
    }

    /// <summary>Probe draws are wanted in this frame.</summary>
    public bool Wanted => _warmup >= WarmupFrames && _drawn < Frames;

    /// <summary>
    /// The probe frames are drawn and enough came back; <see cref="ExtraMilliseconds"/> holds the
    /// result. Half is enough: a disjoint timestamp (GPU clock change) drops a measurement.
    /// </summary>
    public bool Done => _drawn >= Frames && _baseCount >= Frames / 2 && _variantCount >= Frames / 2;

    /// <summary>Mean extra GPU time of the variant over the base, never negative.</summary>
    public double ExtraMilliseconds => Done ? Math.Max(0, (_variantSum / _variantCount) - (_baseSum / _baseCount)) : 0;

    /// <summary>Measures again, e.g. when the whole monitor turned smart (a dearer case than a few windows).</summary>
    public void Restart()
    {
        _warmup = 0;
        _drawn = 0;
        _reported = false;
        _waited = 0;
        _baseCount = 0;
        _variantCount = 0;
        _baseSum = 0;
        _variantSum = 0;
    }

    /// <summary>One frame in which the variant is used; call before <see cref="Wanted"/>.</summary>
    public void Tick()
    {
        if (_warmup < WarmupFrames)
        {
            _warmup++;
        }
    }

    public void BeginBase(ID3D11DeviceContext context) => _baseTimed = _base.Begin(context);

    public void EndBase(ID3D11DeviceContext context)
    {
        if (_baseTimed)
        {
            _base.End(context);
        }
    }

    public void BeginVariant(ID3D11DeviceContext context) => _variantTimed = _variant.Begin(context);

    public void EndVariant(ID3D11DeviceContext context)
    {
        if (_variantTimed)
        {
            _variant.End(context);
        }

        // A frame counts once both draws were timed; a set still pending on the GPU repeats it.
        if (_baseTimed && _variantTimed)
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
        while (_base.TryRead(context, out var plain))
        {
            if (counting)
            {
                _baseSum += plain;
                _baseCount++;
            }
        }

        while (_variant.TryRead(context, out var variant))
        {
            if (counting)
            {
                _variantSum += variant;
                _variantCount++;
            }
        }

        // Too many disjoint results (clock changes): measure once more, then give up quietly
        // (for the smart inversion the budget keeps checking the ambient pass).
        if (_drawn >= Frames && !Done && ++_waited > CollectFrames && !_retried)
        {
            _retried = true;
            Restart();
        }

        // Done can already be true when the last probe frame was drawn (EndVariant), so "became done
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
        _variant.Dispose();
        _base.Dispose();
    }
}
