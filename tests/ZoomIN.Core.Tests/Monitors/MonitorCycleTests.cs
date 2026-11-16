using ZoomIN.Core.Monitors;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Monitors;

/// <summary>Order of Ctrl+Alt+N and the jump on freezing (G2): layout, back and forth, skipping frozen monitors.</summary>
public sealed class MonitorCycleTests
{
    private static readonly PhysicalRect s_left = new(0, 0, 1920, 1080);
    private static readonly PhysicalRect s_middle = new(1920, 0, 1920, 1080);
    private static readonly PhysicalRect s_right = new(3840, 0, 1920, 1080);

    // The grilling example: Windows numbers 1 (left), 3 (middle), 2 (right), listed by number.
    private static readonly PhysicalRect[] s_grilling = [s_left, s_right, s_middle];
    private const int One = 0;
    private const int Two = 1;
    private const int Three = 2;

    [Fact]
    public void GrillingLayout_GoesBackAndForth_MiddleToTheRightFirst()
    {
        var frozen = new bool[3];
        var direction = CycleDirection.Right;
        int[] walk = [Walk(s_grilling, frozen, One, ref direction)];
        for (var i = 0; i < 4; i++)
        {
            walk = [.. walk, Walk(s_grilling, frozen, walk[^1], ref direction)];
        }

        // 1 → 3 → 2 → 3 → 1 → 3
        Assert.Equal([Three, Two, Three, One, Three], walk);

        direction = CycleDirection.Right;
        Assert.Equal(Two, Walk(s_grilling, frozen, Three, ref direction));
    }

    [Fact]
    public void GrillingExample_RightFrozen_FirstSwitchFromTheMiddleGoesLeft_ThenBack()
    {
        bool[] frozen = [false, true, false];
        var direction = CycleDirection.Right;

        Assert.Equal(One, MonitorCycle.Next(s_grilling, frozen, Three, ref direction, out var skipped));
        Assert.Equal(0, skipped);
        Assert.Equal(CycleDirection.Left, direction);
        Assert.Equal(Three, Walk(s_grilling, frozen, One, ref direction));
        Assert.Equal(One, Walk(s_grilling, frozen, Three, ref direction));
    }

    [Fact]
    public void FrozenMiddle_IsSkipped_AndCounted()
    {
        bool[] frozen = [false, false, true];
        var direction = CycleDirection.Right;

        Assert.Equal(Two, MonitorCycle.Next(s_grilling, frozen, One, ref direction, out var skipped));
        Assert.Equal(1, skipped);
        Assert.Equal(One, MonitorCycle.Next(s_grilling, frozen, Two, ref direction, out skipped));
        Assert.Equal(1, skipped);
    }

    [Fact]
    public void OneMonitor_OrNoOtherFreeMonitor_GivesNone()
    {
        var direction = CycleDirection.Right;
        Assert.Equal(-1, MonitorCycle.Next([s_left], [false], 0, ref direction, out _));
        Assert.Equal(-1, MonitorCycle.Next(s_grilling, [false, true, true], One, ref direction, out var skipped));
        Assert.Equal(0, skipped);
        Assert.Equal(CycleDirection.Right, direction);
    }

    [Fact]
    public void TwoMonitors_Alternate()
    {
        PhysicalRect[] monitors = [s_middle, s_left];
        var direction = CycleDirection.Right;

        Assert.Equal(0, Walk(monitors, [false, false], 1, ref direction));
        Assert.Equal(1, Walk(monitors, [false, false], 0, ref direction));
        Assert.Equal(0, Walk(monitors, [false, false], 1, ref direction));
    }

    [Fact]
    public void StackedMonitors_GoTopToBottomInTheirColumn()
    {
        // A laptop under the left monitor (same centre x), a third monitor to the right.
        PhysicalRect[] monitors = [new(0, 1080, 1920, 1080), s_middle, s_left];
        var direction = CycleDirection.Right;

        Assert.Equal(0, Walk(monitors, new bool[3], 2, ref direction));
        Assert.Equal(1, Walk(monitors, new bool[3], 0, ref direction));
        Assert.Equal(0, Walk(monitors, new bool[3], 1, ref direction));
        Assert.Equal(2, Walk(monitors, new bool[3], 0, ref direction));
    }

    [Fact]
    public void UnknownStart_TakesTheFirstFreeMonitorInLayoutOrder()
    {
        var direction = CycleDirection.Left;

        Assert.Equal(One, MonitorCycle.Next(s_grilling, new bool[3], -1, ref direction, out _));
        Assert.Equal(Three, MonitorCycle.Next(s_grilling, [true, false, false], 7, ref direction, out _));
    }

    [Fact]
    public void NearestFree_PicksTheClosestNeighbour_RightOnATie()
    {
        // Pointer in the exact middle of the middle monitor: both neighbours are 960 px away.
        Assert.Equal(Two, MonitorCycle.NearestFree(s_grilling, [false, false, true], new PhysicalPoint(2880, 540)));
        Assert.Equal(One, MonitorCycle.NearestFree(s_grilling, [false, false, true], new PhysicalPoint(2000, 540)));
        Assert.Equal(Three, MonitorCycle.NearestFree(s_grilling, [false, true, false], new PhysicalPoint(5000, 100)));
        Assert.Equal(One, MonitorCycle.NearestFree(s_grilling, [false, true, true], new PhysicalPoint(5000, 100)));
        Assert.Equal(-1, MonitorCycle.NearestFree(s_grilling, [true, true, true], new PhysicalPoint(5000, 100)));
    }

    [Fact]
    public void MismatchedFlags_AreRejected()
    {
        var direction = CycleDirection.Right;
        Assert.Throws<ArgumentException>(() => MonitorCycle.Next(s_grilling, new bool[2], 0, ref direction, out _));
        Assert.Throws<ArgumentException>(() => MonitorCycle.NearestFree(s_grilling, new bool[4], default));
    }

    [Fact]
    public void Next_DoesNotAllocate()
    {
        var frozen = new bool[3];
        var direction = CycleDirection.Right;
        var current = One;
        for (var i = 0; i < 10; i++)
        {
            current = Walk(s_grilling, frozen, current, ref direction);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
        {
            current = Walk(s_grilling, frozen, current, ref direction);
            current += MonitorCycle.NearestFree(s_grilling, frozen, new PhysicalPoint(i, 10)) * 0;
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static int Walk(PhysicalRect[] monitors, bool[] frozen, int current, ref CycleDirection direction) =>
        MonitorCycle.Next(monitors, frozen, current, ref direction, out _);
}
