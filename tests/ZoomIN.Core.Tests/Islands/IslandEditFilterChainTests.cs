using System.Diagnostics;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Islands;

/// <summary>
/// The island drag (B1) in the fence's chain with the mouse buffer and axis lock (A6) and a frozen
/// monitor (G2): order 100 → 200 → 300, then the forbidden areas.
/// </summary>
public sealed class IslandEditFilterChainTests
{
    private static readonly PhysicalRect Left = new(0, 0, 1920, 1080);
    private static readonly PhysicalRect Middle = new(1920, 0, 1920, 1080);
    private static readonly PhysicalRect Right = new(3840, 0, 1920, 1080);

    private readonly IslandDragFence _drag = new();
    private readonly AxisLock _axis = new();
    private readonly EdgeBuffer _buffer = new(TimeProvider.System);

    [Fact]
    public void Drag_StopsAtItsMonitorsEdge_TheBufferNeverSeesACrossing()
    {
        _buffer.Configure([Left, Middle, Right], EdgeBufferLevel.Light);
        var decision = Chain(null);
        _drag.Confine(Left);

        // Long enough for the buffer to let a crossing through: the drag still keeps the pointer on its monitor.
        var result = Press(decision, new PhysicalPoint(1910, 500), new PhysicalPoint(1990, 500), TimeSpan.FromSeconds(1));

        Assert.Equal(new PhysicalPoint(1919, 500), result);
        Assert.False(_buffer.IsPressing);

        _drag.Confine(null);
        Assert.Equal(new PhysicalPoint(1919, 500), decision.Decide(Ticks(TimeSpan.Zero), new PhysicalPoint(1910, 500), new PhysicalPoint(1990, 500)));
        Assert.True(_buffer.IsPressing); // after the drag the buffer is back in charge
    }

    [Fact]
    public void AxisLock_KeepsTheRow_WithinTheDragsMonitor()
    {
        var decision = Chain(null);
        _drag.Confine(Left);
        _axis.SetHeld(true);

        var at = Ticks(TimeSpan.Zero);
        decision.Decide(at, new PhysicalPoint(1000, 500), new PhysicalPoint(1020, 503));
        var result = decision.Decide(at, new PhysicalPoint(1020, 500), new PhysicalPoint(2500, 530));

        Assert.Equal(500, result.Y);
        Assert.Equal(1919, result.X);
    }

    [Fact]
    public void FrozenMonitor_StaysForbidden_AlsoForADragConfinedToIt()
    {
        var decision = Chain(FenceGeometry.Create([Left, Middle, Right], [Right]));

        // A drag on the live middle monitor stops at its edge, before the frozen one.
        _drag.Confine(Middle);
        Assert.Equal(new PhysicalPoint(3839, 10), decision.Decide(0, new PhysicalPoint(3830, 10), new PhysicalPoint(3900, 10)));

        // Even a drag fence that pointed at the frozen monitor cannot let the pointer onto it.
        _drag.Confine(Right);
        var result = decision.Decide(0, new PhysicalPoint(3830, 10), new PhysicalPoint(3900, 10));
        Assert.False(Right.Contains(result));
    }

    private static long Ticks(TimeSpan time) => (long)(time.TotalSeconds * Stopwatch.Frequency);

    private static PhysicalPoint Press(FenceDecision decision, PhysicalPoint from, PhysicalPoint to, TimeSpan duration)
    {
        var current = from;
        for (var ms = 0; ms <= duration.TotalMilliseconds; ms += 20)
        {
            current = decision.Decide(Ticks(TimeSpan.FromMilliseconds(ms)), current, to);
        }

        return current;
    }

    private FenceDecision Chain(FenceGeometry? geometry) => new(geometry, [_buffer, _axis, _drag]);
}
