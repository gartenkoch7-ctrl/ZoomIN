using System.Diagnostics;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.PointerFence;

/// <summary>
/// M19 integration: mouse buffer and axis lock (A6) next to a frozen monitor (G2). The buffer may let a
/// crossing through after its pressing time, the forbidden area still keeps the pointer off the frozen
/// monitor; between free monitors the buffer works as before.
/// </summary>
public sealed class FrozenMonitorChainTests
{
    private static readonly PhysicalRect Left = new(0, 0, 1920, 1080);
    private static readonly PhysicalRect Middle = new(1920, 0, 1920, 1080);
    private static readonly PhysicalRect Right = new(3840, 0, 1920, 1080);

    private readonly EdgeBuffer _buffer = new(TimeProvider.System);
    private readonly AxisLock _axis = new();

    [Fact]
    public void Buffer_AtTheEdgeOfAFrozenMonitor_NeverLetsThePointerOn()
    {
        _buffer.Configure([Left, Middle, Right], EdgeBufferLevel.Light);
        var decision = Chain([Right]);

        // Far longer than the light level's 0.2 s: the buffer gives way, the frozen monitor does not.
        var end = Push(decision, new PhysicalPoint(3830, 500), 20, TimeSpan.FromSeconds(2), out var everOn);

        Assert.False(everOn);
        Assert.Equal(new PhysicalPoint(3839, 500), end);
    }

    [Fact]
    public void Buffer_BetweenFreeMonitors_StillCrosses_WhileAnotherIsFrozen()
    {
        _buffer.Configure([Left, Middle, Right], EdgeBufferLevel.Light);
        var decision = Chain([Right]);

        // First held at the edge, then across after the pressing time.
        Assert.Equal(new PhysicalPoint(1919, 500), decision.Decide(Ticks(0), new PhysicalPoint(1910, 500), new PhysicalPoint(1930, 500)));
        var end = Push(decision, new PhysicalPoint(1919, 500), 20, TimeSpan.FromSeconds(1), out _);

        Assert.True(Middle.Contains(end));
    }

    [Fact]
    public void FrozenMiddle_KeepsThePointerOnItsSide_AlsoWithTheBufferGivingWay()
    {
        _buffer.Configure([Left, Middle, Right], EdgeBufferLevel.Light);
        var decision = Chain([Middle]);

        var end = Push(decision, new PhysicalPoint(1900, 500), 40, TimeSpan.FromSeconds(2), out var everOn);

        Assert.False(everOn);
        Assert.True(Left.Contains(end));
    }

    [Fact]
    public void AxisLock_KeepsItsRow_AtTheEdgeOfAFrozenMonitor()
    {
        var decision = Chain([Right]);
        _axis.SetHeld(true);
        decision.Decide(Ticks(0), new PhysicalPoint(3700, 500), new PhysicalPoint(3720, 503)); // chooses the row

        var end = decision.Decide(Ticks(10), new PhysicalPoint(3720, 500), new PhysicalPoint(3900, 530));

        Assert.Equal(new PhysicalPoint(3839, 500), end);
    }

    private static long Ticks(double ms) => (long)(ms / 1000 * Stopwatch.Frequency);

    // A steady push to the right, one move every 20 ms; reports whether the pointer was ever on the frozen side.
    private static PhysicalPoint Push(FenceDecision decision, PhysicalPoint from, double step, TimeSpan duration, out bool everOn)
    {
        var current = from;
        everOn = false;
        for (var ms = 0; ms <= duration.TotalMilliseconds; ms += 20)
        {
            current = decision.Decide(Ticks(ms), current, new PhysicalPoint(current.X + step, current.Y));
            everOn |= decision.Geometry is { } geometry && !geometry.IsAllowed(current);
        }

        return current;
    }

    private FenceDecision Chain(PhysicalRect[] frozen) =>
        new(FenceGeometry.Create([Left, Middle, Right], frozen), [_buffer, _axis]);
}
