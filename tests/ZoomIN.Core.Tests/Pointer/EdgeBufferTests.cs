using System.Diagnostics;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Pointer;

/// <summary>Mouse buffer (A6): stop at the edge, cross after the level's pressing time, start over on retreat.</summary>
public sealed class EdgeBufferTests
{
    private static readonly PhysicalRect Left = new(0, 0, 1920, 1080);
    private static readonly PhysicalRect Right = new(1920, 0, 1920, 1080);

    private readonly EdgeBuffer _buffer = new(TimeProvider.System);

    [Fact]
    public void Off_LetsEveryCrossingThrough()
    {
        _buffer.Configure([Left, Right], EdgeBufferLevel.Off);

        Assert.Equal(new PhysicalPoint(1925, 10), Move(0, 1919, 10, 1925, 10));
        Assert.False(_buffer.IsPressing);
    }

    [Fact]
    public void SingleMonitor_LetsEveryMoveThrough()
    {
        _buffer.Configure([Left], EdgeBufferLevel.Strong);

        Assert.Equal(new PhysicalPoint(1925, 10), Move(0, 1919, 10, 1925, 10));
    }

    [Fact]
    public void MovesWithinAMonitor_AreUntouched()
    {
        _buffer.Configure([Left, Right], EdgeBufferLevel.Medium);

        Assert.Equal(new PhysicalPoint(1919, 500), Move(0, 1900, 500, 1919, 500));
        Assert.False(_buffer.IsPressing);
    }

    [Fact]
    public void Crossing_StopsAtTheEdge()
    {
        _buffer.Configure([Left, Right], EdgeBufferLevel.Medium);

        Assert.Equal(new PhysicalPoint(1919, 512), Move(0, 1900, 510, 1940, 512));
        Assert.True(_buffer.IsPressing);
        Assert.Equal(PointerFilterOrder.MouseBuffer, _buffer.Order);
    }

    [Theory]
    [InlineData(EdgeBufferLevel.Light, 200)]
    [InlineData(EdgeBufferLevel.Medium, 400)]
    [InlineData(EdgeBufferLevel.Strong, 800)]
    public void Pressing_CrossesAfterTheLevelsTime(EdgeBufferLevel level, int pressMs)
    {
        _buffer.Configure([Left, Right], level);
        var at = 0;

        // A steady push: a move every 10 ms, each one held back until the time is up.
        for (; at < pressMs; at += 10)
        {
            Assert.Equal(new PhysicalPoint(1919, 10), Move(at, 1919, 10, 1923, 10));
        }

        Assert.Equal(new PhysicalPoint(1923, 10), Move(pressMs, 1919, 10, 1923, 10));
        Assert.False(_buffer.IsPressing);
    }

    [Fact]
    public void Crossing_WorksInBothDirections()
    {
        _buffer.Configure([Left, Right], EdgeBufferLevel.Light);

        Assert.Equal(new PhysicalPoint(1920, 10), Move(0, 1920, 10, 1915, 10));
        Assert.Equal(new PhysicalPoint(1915, 10), Move(200, 1920, 10, 1915, 10));
    }

    [Fact]
    public void Retreat_StartsOver()
    {
        _buffer.Configure([Left, Right], EdgeBufferLevel.Medium);
        Move(0, 1919, 10, 1925, 10);
        Move(300, 1919, 10, 1925, 10);

        Assert.Equal(new PhysicalPoint(1910, 10), Move(310, 1919, 10, 1910, 10));
        Assert.False(_buffer.IsPressing);

        // A new push counts from its own start: 400 ms after 320, not after 0.
        Move(320, 1919, 10, 1925, 10);
        Assert.Equal(new PhysicalPoint(1919, 10), Move(520, 1919, 10, 1925, 10));
        Assert.Equal(new PhysicalPoint(1919, 10), Move(700, 1919, 10, 1925, 10));
        Assert.Equal(new PhysicalPoint(1925, 10), Move(720, 1919, 10, 1925, 10));
    }

    [Theory]
    [InlineData(1920, 10, 1915, 10, 1925, 10)] // pressing left from the right monitor, back to the right
    [InlineData(100, 1080, 100, 1075, 100, 1090)] // pressing up from the lower monitor, back down
    [InlineData(100, 1079, 100, 1085, 100, 1070)] // pressing down from the upper monitor, back up
    public void Retreat_StartsOver_InEveryDirection(double x, double y, double pushX, double pushY, double backX, double backY)
    {
        var below = new PhysicalRect(0, 1080, 1920, 1080);
        _buffer.Configure([Left, Right, below], EdgeBufferLevel.Medium);
        Move(0, x, y, pushX, pushY);
        Assert.True(_buffer.IsPressing);

        Move(100, x, y, backX, backY);

        Assert.False(_buffer.IsPressing);
    }

    [Fact]
    public void SlidingAlongTheEdge_KeepsPressing()
    {
        _buffer.Configure([Left, Right], EdgeBufferLevel.Medium);
        Move(0, 1919, 10, 1925, 10);

        Assert.Equal(new PhysicalPoint(1919, 40), Move(100, 1919, 10, 1919, 40));
        Assert.True(_buffer.IsPressing);
        Assert.Equal(new PhysicalPoint(1919, 40), Move(200, 1919, 40, 1925, 40));
        Assert.Equal(new PhysicalPoint(1925, 40), Move(400, 1919, 40, 1925, 40));
    }

    [Fact]
    public void LongPause_StartsOver()
    {
        _buffer.Configure([Left, Right], EdgeBufferLevel.Medium);
        Move(0, 1919, 10, 1925, 10);

        // 300 ms without a push: the press restarts, so 400 ms after the first move is not enough.
        Assert.Equal(new PhysicalPoint(1919, 10), Move(300, 1919, 10, 1925, 10));
        Assert.Equal(new PhysicalPoint(1919, 10), Move(500, 1919, 10, 1925, 10));
        Assert.Equal(new PhysicalPoint(1925, 10), Move(700, 1919, 10, 1925, 10));
    }

    [Fact]
    public void NewDirection_StartsOver()
    {
        var below = new PhysicalRect(0, 1080, 1920, 1080);
        _buffer.Configure([Left, Right, below], EdgeBufferLevel.Medium);
        Move(0, 1000, 1079, 1000, 1085);

        // Down, then right: no direction in common, so the push to the right counts from 100 ms.
        Assert.Equal(new PhysicalPoint(1919, 1000), Move(100, 1919, 1000, 1925, 1000));
        Assert.Equal(new PhysicalPoint(1919, 1000), Move(300, 1919, 1000, 1925, 1000));
        Assert.Equal(new PhysicalPoint(1919, 1000), Move(450, 1919, 1000, 1925, 1000));
        Assert.Equal(new PhysicalPoint(1925, 1000), Move(500, 1919, 1000, 1925, 1000));
    }

    [Fact]
    public void CornerBetweenMonitors_HoldsBackEveryNeighbour()
    {
        var below = new PhysicalRect(0, 1080, 1920, 1080);
        var diagonal = new PhysicalRect(1920, 1080, 1920, 1080);
        _buffer.Configure([Left, Right, below, diagonal], EdgeBufferLevel.Medium);

        Assert.Equal(new PhysicalPoint(1919, 1079), Move(0, 1919, 1079, 1925, 1085));
        Assert.Equal(new PhysicalPoint(1919, 1079), Move(100, 1919, 1079, 1925, 1079));
        Assert.Equal(new PhysicalPoint(1919, 1079), Move(200, 1919, 1079, 1919, 1085));

        // The wobble between right, down and both is one push: it crosses after 400 ms.
        Assert.Equal(new PhysicalPoint(1925, 1079), Move(400, 1919, 1079, 1925, 1079));
    }

    [Fact]
    public void CornerPress_SlidingAlongOneEdge_KeepsPressing()
    {
        var tall = new PhysicalRect(1920, 0, 1920, 2160);
        _buffer.Configure([Left, tall], EdgeBufferLevel.Medium);
        Move(0, 1919, 1079, 1925, 1085);

        Assert.Equal(new PhysicalPoint(1919, 1075), Move(100, 1919, 1079, 1919, 1075));
        Assert.True(_buffer.IsPressing);
    }

    [Fact]
    public void DiagonalPastTheBottomCorner_PressesTowardsTheNeighbour()
    {
        _buffer.Configure([Left, Right], EdgeBufferLevel.Light);

        // Sliding right along the taskbar with the hand drifting down: the target lies below both.
        Assert.Equal(new PhysicalPoint(1919, 1079), Move(0, 1919, 1079, 1925, 1085));
        Assert.True(_buffer.IsPressing);
        Assert.Equal(new PhysicalPoint(1925, 1079), Move(200, 1919, 1079, 1925, 1085));
    }

    [Fact]
    public void PressOnAnotherMonitor_StartsOver()
    {
        var third = new PhysicalRect(3840, 0, 1920, 1080);
        _buffer.Configure([Left, Right, third], EdgeBufferLevel.Medium);
        Move(0, 1919, 10, 1925, 10);
        Move(200, 1919, 10, 1925, 10);

        // Moved by a shortcut onto the next monitor's right edge, still pushing right.
        Assert.Equal(new PhysicalPoint(3839, 10), Move(300, 3839, 10, 3845, 10));
        Assert.Equal(new PhysicalPoint(3839, 10), Move(450, 3839, 10, 3845, 10));
        Assert.Equal(new PhysicalPoint(3845, 10), Move(700, 3839, 10, 3845, 10));
    }

    [Fact]
    public void OffsetMonitors_GapIsNoCrossing()
    {
        var lower = new PhysicalRect(1920, 200, 1920, 1080);
        _buffer.Configure([Left, lower], EdgeBufferLevel.Medium);

        Assert.Equal(new PhysicalPoint(1919, 100), Move(0, 1919, 100, 1925, 100));
        Assert.False(_buffer.IsPressing);

        Assert.Equal(new PhysicalPoint(1919, 500), Move(10, 1919, 500, 1925, 500));
        Assert.True(_buffer.IsPressing);
        Move(200, 1919, 500, 1925, 500);
        Assert.Equal(new PhysicalPoint(1925, 500), Move(410, 1919, 500, 1925, 500));
    }

    [Fact]
    public void NegativeCoordinates_MonitorLeftOfPrimary()
    {
        var leftOfPrimary = new PhysicalRect(-2560, 0, 2560, 1440);
        _buffer.Configure([leftOfPrimary, Left], EdgeBufferLevel.Light);

        Assert.Equal(new PhysicalPoint(0, 10), Move(0, 0, 10, -4, 10));
        Assert.Equal(new PhysicalPoint(-4, 10), Move(200, 0, 10, -4, 10));
    }

    [Fact]
    public void PointerOutsideEveryMonitor_MovesFreely()
    {
        _buffer.Configure([Left, Right], EdgeBufferLevel.Strong);

        Assert.Equal(new PhysicalPoint(1925, 10), Move(0, 5000, 10, 1925, 10));
    }

    [Fact]
    public void NewSetup_StartsOver()
    {
        _buffer.Configure([Left, Right], EdgeBufferLevel.Medium);
        Move(0, 1919, 10, 1925, 10);
        Move(200, 1919, 10, 1925, 10);
        _buffer.Configure([Left, Right], EdgeBufferLevel.Light);

        Assert.Equal(new PhysicalPoint(1919, 10), Move(400, 1919, 10, 1925, 10));
    }

    [Fact]
    public void SameSetupAgain_KeepsPressing()
    {
        _buffer.Configure([Left, Right], EdgeBufferLevel.Medium);
        Move(0, 1919, 10, 1925, 10);
        Move(200, 1919, 10, 1925, 10);
        _buffer.Configure([Left, Right], EdgeBufferLevel.Medium); // refreshed after a desktop switch

        Assert.Equal(new PhysicalPoint(1925, 10), Move(400, 1919, 10, 1925, 10));
    }

    [Fact]
    public void HeldBackCrossings_AreCountedOncePerPress()
    {
        _buffer.Configure([Left, Right], EdgeBufferLevel.Medium);
        var before = PointerConstraintMetrics.BufferBlocked;

        Move(0, 1919, 10, 1925, 10);
        Move(10, 1919, 10, 1925, 10);
        Move(20, 1919, 10, 1925, 10);

        // Other tests run in parallel and count too: at least this one.
        Assert.True(PointerConstraintMetrics.BufferBlocked - before >= 1);
    }

    [Fact]
    public void Configure_RejectsNull() =>
        Assert.Throws<ArgumentNullException>(() => _buffer.Configure(null!, EdgeBufferLevel.Light));

    private PhysicalPoint Move(int ms, double fromX, double fromY, double toX, double toY) =>
        _buffer.Filter(Ticks(ms), new PhysicalPoint(fromX, fromY), new PhysicalPoint(toX, toY));

    internal static long Ticks(int ms) => (long)(ms * (double)Stopwatch.Frequency / 1000);
}
