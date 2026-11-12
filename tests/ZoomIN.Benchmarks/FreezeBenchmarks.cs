using BenchmarkDotNet.Attributes;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Benchmarks;

/// <summary>Frozen monitors (G2): the per-frame lookup of a render thread and the monitor cycle. Budget: 0 B.</summary>
public class FreezeBenchmarks
{
    private readonly ZoomModel _model = new(new ZoomLevels(), ZoomMode.Fullscreen, true, 0.15);
    private readonly PhysicalRect[] _monitors = [new(0, 0, 1920, 1080), new(3840, 0, 1920, 1080), new(1920, 0, 1920, 1080)];
    private readonly bool[] _frozen = [false, true, false];
    private CycleDirection _direction = CycleDirection.Right;
    private int _current;

    [GlobalSetup]
    public void Setup()
    {
        var monitor = new FrozenMonitor(@"\\.\DISPLAY2", _monitors[1], 4, ZoomMode.Fullscreen, LensSize.Default, DockLayout.Default, false, RgbColor.Cyan);
        monitor.KeepAnchor(new PhysicalPoint(4000, 300));
        _model.Frozen.Freeze(monitor);
    }

    /// <summary>What every render thread reads per frame: its freeze (here: frozen and not frozen) and the kept anchor.</summary>
    [Benchmark]
    public double FrozenLookup()
    {
        var frozen = _model.Frozen.For(@"\\.\DISPLAY2");
        var live = _model.Frozen.For(@"\\.\DISPLAY1");
        return (frozen is not null && frozen.TryGetAnchor(out var anchor) ? anchor.X : 0) + (live?.Level ?? 0);
    }

    /// <summary>Ctrl+Alt+N with the right monitor frozen.</summary>
    [Benchmark]
    public int MonitorCycleNext()
    {
        _current = MonitorCycle.Next(_monitors, _frozen, _current, ref _direction, out _);
        return _current;
    }
}
