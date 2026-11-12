using BenchmarkDotNet.Attributes;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Benchmarks;

/// <summary>
/// More Core code the render loop calls every frame (M14 step 1): view geometry of lens and docked
/// mode, the smart-region check and the pointer size. Budget: 0 B allocated.
/// </summary>
public class FrameGeometryBenchmarks
{
    private readonly PhysicalRect _monitor = new(0, 0, 3840, 2160);
    private readonly PointerStyle _pointer = PointerStyle.Default with { UnzoomedScale = 2 };

    // Worst case: the full shader limit of regions, the last one smart, all on the monitor.
    private readonly WindowRegions _windows = new(
        Enumerable.Range(0, WindowRegions.Max).Select(i => new PhysicalRect(i * 20, i * 10, 600, 400)).ToArray(),
        Enumerable.Range(0, WindowRegions.Max)
            .Select(i => i == WindowRegions.Max - 1 ? WindowPalette.Smart : WindowPalette.For(FilterId.Inverted))
            .ToArray());

    private readonly TextEnhancement _enhancement = new(0.6, 1);

    private int _frame;

    [Benchmark]
    public double LensFrame()
    {
        _frame++;
        return ModeGeometry.Compute(ZoomMode.Lens, _monitor, Cursor(), 4.0, LensSize.Default, DockLayout.Default).Source.X;
    }

    [Benchmark]
    public double DockedFrame()
    {
        _frame++;
        return ModeGeometry.Compute(ZoomMode.Docked, _monitor, Cursor(), 4.0, LensSize.Default, DockLayout.Default).Source.X;
    }

    /// <summary>Does any region on this monitor need the smart weights (DesktopLayer, every frame)?</summary>
    [Benchmark]
    public bool SmartRegionsCheck() => _windows.UsesSmart(_monitor);

    /// <summary>Pointer size and whether ZoomIN draws it (CursorLayer, every frame).</summary>
    [Benchmark]
    public double PointerScale()
    {
        _frame++;
        var zoom = 1 + (_frame % 16);
        return _pointer.DrawsPointer(zoom) ? _pointer.ScaleFor(zoom) : 0;
    }

    /// <summary>"Schärfe"/"Fett" resolved for the zoom and smoothing (DesktopLayer, every frame; M18, D1).</summary>
    [Benchmark]
    public double TextEnhancementAt()
    {
        _frame++;
        var zoom = 1 + (_frame % 8 * 0.25);
        var frame = ModeGeometry.Compute(ZoomMode.Docked, _monitor, Cursor(), zoom, LensSize.Default, DockLayout.Default);
        return _enhancement.At(frame, Smoothing.Text).BoldPixels;
    }

    private PhysicalPoint Cursor() => new(_frame % 3840, _frame % 2160);
}
