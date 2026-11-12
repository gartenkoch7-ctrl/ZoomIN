using ZoomIN.Core.Pointer;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Tests.Pointer;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.HotPath;

/// <summary>
/// Mouse buffer and axis lock run inside the low-level mouse hook for every move (A6): 0 B,
/// including holding back, crossing, choosing the axis and counting.
/// </summary>
public sealed class PointerConstraintAllocationTests
{
    private const int Moves = 10_000;
    private static readonly PhysicalRect[] Monitors = [new(0, 0, 1920, 1080), new(1920, 0, 1920, 1080), new(0, 1080, 1920, 1080)];

    [Fact]
    public void EdgeBuffer_DoesNotAllocate()
    {
        var buffer = new EdgeBuffer(TimeProvider.System);
        buffer.Configure(Monitors, EdgeBufferLevel.Light);
        var sum = Push(buffer, 0, 0); // warm-up / JIT

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Moves; i++)
        {
            sum += Push(buffer, i, i * 10);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sum > 0);
    }

    [Fact]
    public void AxisLock_DoesNotAllocate()
    {
        var axis = new AxisLock();
        axis.SetHeld(true);
        var sum = axis.Filter(0, new PhysicalPoint(100, 100), new PhysicalPoint(120, 101)).X; // warm-up / JIT

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Moves; i++)
        {
            // Press and release every 50 moves: anchor, threshold, axis choice and counting all run.
            axis.SetHeld(i % 50 != 0);
            sum += axis.Filter(0, new PhysicalPoint(100 + (i % 50), 100), new PhysicalPoint(101 + (i % 50), 100 + (i % 7))).Y;
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sum > 0);
    }

    [Fact]
    public void FenceDecision_WithBothFilters_DoesNotAllocate()
    {
        var buffer = new EdgeBuffer(TimeProvider.System);
        buffer.Configure(Monitors, EdgeBufferLevel.Medium);
        var axis = new AxisLock();
        axis.SetHeld(true);
        var geometry = FenceGeometry.Create(Monitors, [Monitors[2]]);
        var decision = new FenceDecision(geometry, [buffer, axis]);
        var sum = decision.Decide(0, new PhysicalPoint(1900, 10), new PhysicalPoint(1930, 12)).X; // warm-up / JIT

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Moves; i++)
        {
            sum += decision.Decide(EdgeBufferTests.Ticks(i * 10), new PhysicalPoint(1900 + (i % 20), 10), new PhysicalPoint(1915 + (i % 20), 12)).X;
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sum > 0);
    }

    // Pushes against the edge to the right monitor and, every few moves, crosses or retreats.
    private static double Push(EdgeBuffer buffer, int i, int ms)
    {
        var to = (i % 30) == 29 ? 1910 : 1925;
        return buffer.Filter(EdgeBufferTests.Ticks(ms), new PhysicalPoint(1919, i % 1000), new PhysicalPoint(to, i % 1000)).X;
    }
}
