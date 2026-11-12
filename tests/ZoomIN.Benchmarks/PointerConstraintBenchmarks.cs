using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Benchmarks;

/// <summary>
/// What the low-level mouse hook does per move for mouse buffer and axis lock (A6). Budget: 0 B
/// allocated and far below a millisecond (Windows removes slow hooks).
/// </summary>
public class PointerConstraintBenchmarks
{
    private static readonly PhysicalRect[] Monitors = [new(0, 0, 1920, 1080), new(1920, 0, 1920, 1080)];
    private readonly EdgeBuffer _buffer = new(TimeProvider.System);
    private readonly AxisLock _axis = new();
    private long _move;

    [GlobalSetup]
    public void Setup()
    {
        _buffer.Configure(Monitors, EdgeBufferLevel.Medium);
        _axis.SetHeld(true);
    }

    [Benchmark]
    public double EdgeBufferPush()
    {
        _move++;
        return _buffer.Filter(Stopwatch.GetTimestamp(), new PhysicalPoint(1919, _move % 1000), new PhysicalPoint(1925, _move % 1000)).X;
    }

    [Benchmark]
    public double AxisLockMove()
    {
        _move++;
        return _axis.Filter(0, new PhysicalPoint(100 + (_move % 500), 100), new PhysicalPoint(103 + (_move % 500), 101)).Y;
    }
}
