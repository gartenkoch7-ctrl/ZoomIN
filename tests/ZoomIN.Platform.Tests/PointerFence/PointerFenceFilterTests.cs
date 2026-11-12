using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;
using ZoomIN.Platform.PointerFence;

namespace ZoomIN.Platform.Tests.PointerFence;

/// <summary>The hook path with move filters: broken filters never stop the pointer, forbidden areas still win, 0 B.</summary>
public sealed class PointerFenceFilterTests
{
    private readonly FakeFenceNative _native = new();
    private readonly FakeMonitors _monitors = new();

    [Fact]
    public void FilterThrows_LetsTheMoveThrough_AndIsReported()
    {
        using var fence = Create();
        fence.AddFilter(new Broken());
        var target = _native.Target!;

        Assert.False(target.OnMove(new PhysicalPoint(1, 1), new PhysicalPoint(5, 5), out var allowed));
        Assert.Equal(new PhysicalPoint(5, 5), allowed);
        ((IFenceEvents)fence).OnCheck();
    }

    [Fact]
    public void FilterThrows_ForbiddenAreasStillWin()
    {
        using var fence = Create();
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Middle]);
        fence.AddFilter(new Broken());

        Assert.True(_native.Target!.OnMove(new PhysicalPoint(1910, 5), new PhysicalPoint(1950, 5), out var allowed));
        Assert.Equal(new PhysicalPoint(1919, 5), allowed);
    }

    [Fact]
    public void FilterReturnsNaN_IsTreatedAsAFailure()
    {
        using var fence = Create();
        fence.AddFilter(new NotANumber());

        Assert.False(_native.Target!.OnMove(new PhysicalPoint(1, 1), new PhysicalPoint(5, 5), out var allowed));
        Assert.Equal(new PhysicalPoint(5, 5), allowed);
    }

    [Fact]
    public void HookPath_DoesNotAllocate()
    {
        using var fence = Create();
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Middle, new PhysicalRect(100, 100, 50, 50)]);
        fence.AddFilter(new Nudge());
        var target = _native.Target!;
        target.OnMove(new PhysicalPoint(1900, 10), new PhysicalPoint(1930, 10), out _); // warm-up / JIT

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
        {
            target.OnMove(new PhysicalPoint(1900 + (i % 19), i % 1000), new PhysicalPoint(1905 + (i % 40), i % 1000), out _);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private Win32PointerFence Create() => new(NullLogger.Instance, _monitors, _native, TimeProvider.System);

    private sealed class Broken : IPointerMoveFilter
    {
        public int Order => PointerFilterOrder.MouseBuffer;

        public PhysicalPoint Filter(long timestamp, PhysicalPoint current, PhysicalPoint requested) =>
            throw new InvalidOperationException("filter failed");
    }

    private sealed class NotANumber : IPointerMoveFilter
    {
        public int Order => PointerFilterOrder.MouseBuffer;

        public PhysicalPoint Filter(long timestamp, PhysicalPoint current, PhysicalPoint requested) => new(double.NaN, requested.Y);
    }

    private sealed class Nudge : IPointerMoveFilter
    {
        public int Order => PointerFilterOrder.AxisLock;

        public PhysicalPoint Filter(long timestamp, PhysicalPoint current, PhysicalPoint requested) => requested with { Y = current.Y };
    }
}
