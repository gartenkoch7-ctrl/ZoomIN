using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.PointerFence;

public sealed class FenceDecisionTests
{
    private static readonly PhysicalRect Left = new(0, 0, 1920, 1080);
    private static readonly PhysicalRect Middle = new(1920, 0, 1920, 1080);
    private static readonly PhysicalRect Right = new(3840, 0, 1920, 1080);

    [Fact]
    public void Open_LetsEverythingThrough()
    {
        var decision = FenceDecision.Open;

        Assert.Equal(FenceMode.Off, decision.Mode);
        Assert.Null(decision.Geometry);
        Assert.Equal(new PhysicalPoint(5, 6), decision.Decide(0, new PhysicalPoint(1, 2), new PhysicalPoint(5, 6)));
    }

    [Fact]
    public void Mode_FollowsTheShapeAndTheFilters()
    {
        Assert.Equal(FenceMode.Clip, new FenceDecision(FenceGeometry.Create([Left, Middle, Right], [Right]), []).Mode);
        Assert.Equal(FenceMode.Hook, new FenceDecision(FenceGeometry.Create([Left, Middle, Right], [Middle]), []).Mode);
        Assert.Equal(FenceMode.Hook, new FenceDecision(null, [new Shift(PointerFilterOrder.MouseBuffer, 0)]).Mode);
        Assert.Equal(FenceMode.Off, new FenceDecision(FenceGeometry.Create([Left], []), []).Mode);
    }

    [Fact]
    public void EmptyArea_IsNeverEnforced()
    {
        var decision = new FenceDecision(FenceGeometry.Create([Left], [Left]), []);

        Assert.Equal(FenceMode.Off, decision.Mode);
        Assert.Null(decision.Geometry);
    }

    [Fact]
    public void Filters_RunInTheirOrder_RegardlessOfRegistration()
    {
        var log = new List<int>();
        var decision = new FenceDecision(null, [
            new Shift(PointerFilterOrder.MouseBuffer, 1, log),
            new Shift(PointerFilterOrder.IslandEdit, 10, log),
            new Shift(PointerFilterOrder.AxisLock, 100, log)]);

        var result = decision.Decide(0, default, new PhysicalPoint(0, 0));

        Assert.Equal([PointerFilterOrder.IslandEdit, PointerFilterOrder.AxisLock, PointerFilterOrder.MouseBuffer], log);
        Assert.Equal(new PhysicalPoint(111, 0), result);
        Assert.Equal(3, decision.FilterCount);
    }

    [Fact]
    public void ForbiddenArea_WinsOverEveryFilter()
    {
        var geometry = FenceGeometry.Create([Left, Middle, Right], [Middle]);
        var decision = new FenceDecision(geometry, [new Shift(PointerFilterOrder.MouseBuffer, 500)]);

        var result = decision.Decide(0, new PhysicalPoint(1900, 10), new PhysicalPoint(1910, 10));

        Assert.Equal(new PhysicalPoint(1919, 10), result);
    }

    [Fact]
    public void Decide_PassesTheTimestampToTheFilters()
    {
        var filter = new Shift(PointerFilterOrder.AxisLock, 0);
        new FenceDecision(null, [filter]).Decide(4711, default, default);

        Assert.Equal(4711, filter.LastTimestamp);
    }

    [Fact]
    public void Decide_DoesNotAllocate()
    {
        var geometry = FenceGeometry.Create([Left, Middle, Right], [Middle, new PhysicalRect(100, 100, 50, 50)]);
        var decision = new FenceDecision(geometry, [new Shift(PointerFilterOrder.AxisLock, 1), new Shift(PointerFilterOrder.MouseBuffer, -1)]);
        decision.Decide(0, new PhysicalPoint(1900, 10), new PhysicalPoint(1930, 10)); // warm-up / JIT

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
        {
            decision.Decide(i, new PhysicalPoint(1900 + (i % 20), i % 1000), new PhysicalPoint(1910 + (i % 40), i % 1000));
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void Constructor_RejectsNullFilters()
    {
        Assert.Throws<ArgumentNullException>(() => new FenceDecision(null, null!));
    }

    private sealed class Shift(int order, double dx, List<int>? log = null) : IPointerMoveFilter
    {
        public int Order => order;

        public long LastTimestamp { get; private set; }

        public PhysicalPoint Filter(long timestamp, PhysicalPoint current, PhysicalPoint requested)
        {
            LastTimestamp = timestamp;
            log?.Add(order);
            return requested with { X = requested.X + dx };
        }
    }
}
