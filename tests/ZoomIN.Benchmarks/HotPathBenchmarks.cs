using BenchmarkDotNet.Attributes;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Benchmarks;

/// <summary>Core code called once per frame by the render loop. Budget: 0 B allocated.</summary>
public class HotPathBenchmarks
{
    private readonly PhysicalRect _monitor = new(0, 0, 3840, 2160);
    private readonly ZoomAnimator _animator = new();
    private readonly ZoomModel _model = new(new ZoomLevels(), Core.Modes.ZoomMode.Fullscreen, true, 0.15);
    private readonly EffectsModel _effects = new(DisplayEffects.Default with
    {
        MonitorFilters = new Dictionary<string, MonitorFilter> { [@"\\.\DISPLAY1"] = new(true, ColorFilter.YellowOnBlack) },
    });

    private int _frame;

    [Benchmark]
    public PhysicalRect ViewportSourceRect()
    {
        _frame++;
        return ViewportCalculator.SourceRect(_monitor, new PhysicalPoint(_frame % 3840, _frame % 2160), 4.0);
    }

    [Benchmark]
    public double AnimatorAdvance()
    {
        _animator.SetTarget(++_frame % 2 == 0 ? 8 : 1);
        _animator.Advance(1.0 / 144, 0.15);
        return _animator.Current;
    }

    [Benchmark]
    public double ModelRead() => _model.TargetFor(@"\\.\DISPLAY1") + _model.AnimationSeconds;

    /// <summary>Per-monitor filter lookup done by every render thread each frame.</summary>
    [Benchmark]
    public float EffectsRead() =>
        _effects.CurrentFor(@"\\.\DISPLAY1").Matrix.M00 + _effects.CurrentFor(@"\\.\DISPLAY2").Matrix.M00;
}
