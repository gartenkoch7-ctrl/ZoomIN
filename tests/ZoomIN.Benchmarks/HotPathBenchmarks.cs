using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
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

    private static readonly TrackingOptions s_centerMouse = TrackingOptions.Default with { MouseFollow = MouseFollowMode.Center };

    private readonly AnchorController _anchors = new();
    private readonly AnchorController _marginAnchors = new();

    // Worst case of the filter per window: the full shader limit, none on the monitor asked about.
    private readonly WindowRegions _windows = new(
        Enumerable.Range(0, WindowRegions.Max).Select(i => new PhysicalRect(4000 + (i * 20), i * 10, 600, 400)).ToArray(),
        Enumerable.Range(0, WindowRegions.Max).Select(i => WindowPalette.For(ColorFilter.Inverted)).ToArray());

    // Published by the tracking thread, not per frame. Only the first round is news; afterwards
    // this measures the per-frame cost while following (the allocation test covers every path).
    private readonly TrackingSnapshot[] _carets = Enumerable.Range(1, 64)
        .Select(i => new TrackingSnapshot(null, 0, new PhysicalRect(1500 + (i * 9), 1200, 2, 30), i))
        .ToArray();

    private PointerPlacement _placement;
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

    /// <summary>Pointer image placement per frame (mirrored at the bottom/right edge).</summary>
    [Benchmark]
    public double PointerPlace()
    {
        _frame++;
        _placement = PointerPlacement.Place(
            new PhysicalPoint(_frame % 3840, _frame % 2160), 128, 128, 4, 4, _monitor, _placement);
        return _placement.X;
    }

    /// <summary>Anchor decision per frame while the caret moves (push at the margin, glide).</summary>
    [Benchmark]
    public double AnchorFollowCaret()
    {
        _frame++;
        var now = _frame * (Stopwatch.Frequency / 144);
        var tracking = _carets[_frame % _carets.Length];
        return _anchors.Next(new AnchorInput(
            now, new PhysicalPoint(300, 300), true, _monitor, ZoomMode.Fullscreen, 4, LensSize.Default, DockLayout.Default,
            tracking, TrackingOptions.Default)).X;
    }

    /// <summary>Anchor per frame while the mouse moves, kept in the middle of the view (ADR-0018).</summary>
    [Benchmark]
    public double AnchorCenterMouse()
    {
        _frame++;
        var now = _frame * (Stopwatch.Frequency / 144);
        return _anchors.Next(new AnchorInput(
            now, new PhysicalPoint(_frame % 3840, _frame % 2160), true, _monitor, ZoomMode.Fullscreen, 4, LensSize.Default,
            DockLayout.Default, TrackingSnapshot.Empty, s_centerMouse)).X;
    }

    /// <summary>Anchor per frame while the mouse moves with the margin (M17 A1): mostly pushing, sometimes still.</summary>
    [Benchmark]
    public double AnchorMargin()
    {
        _frame++;
        var now = _frame * (Stopwatch.Frequency / 144);
        var x = _frame % 7680;
        return _marginAnchors.Next(new AnchorInput(
            now, new PhysicalPoint(x < 3840 ? x : 7679 - x, (_frame / 3) % 2160), true, _monitor, ZoomMode.Fullscreen, 4,
            LensSize.Default, DockLayout.Default, TrackingSnapshot.Empty, TrackingOptions.Default)).X;
    }

    /// <summary>Per-monitor filter lookup done by every render thread each frame.</summary>
    [Benchmark]
    public float EffectsRead() =>
        _effects.CurrentFor(@"\\.\DISPLAY1").Matrix.M00 + _effects.CurrentFor(@"\\.\DISPLAY2").Matrix.M00;

    /// <summary>Filter per window: does any window on this monitor need the overlay (ADR-0020)?</summary>
    [Benchmark]
    public bool WindowRegionsCheck() => _windows.ChangesMonitor(_monitor, _effects.CurrentFor(@"\\.\DISPLAY1"));
}
