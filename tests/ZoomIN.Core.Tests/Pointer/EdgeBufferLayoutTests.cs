using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Pointer;

/// <summary>Mouse buffer (A6) in monitor layouts: corners, offsets, negative coordinates, pointer moved between monitors.</summary>
public sealed class EdgeBufferLayoutTests
{
    private static readonly PhysicalRect Left = new(0, 0, 1920, 1080);
    private static readonly PhysicalRect Right = new(1920, 0, 1920, 1080);

    private readonly EdgeBuffer _buffer = new(TimeProvider.System);

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
    public void DiagonalIntoTheEmptyCorner_PressesTowardsTheAxisPushedHarder()
    {
        var below = new PhysicalRect(0, 1080, 1920, 1080);
        _buffer.Configure([Left, Right, below], EdgeBufferLevel.Light);

        // Mostly down with a slight drift right: towards the monitor below, not the one on the right.
        Assert.Equal(new PhysicalPoint(1919, 1079), Move(0, 1919, 1079, 1920, 1090));
        Assert.Equal(new PhysicalPoint(1919, 1090), Move(200, 1919, 1079, 1920, 1090));
    }

    [Fact]
    public void UnknownMonitor_IsNeverBlocked()
    {
        // A monitor plugged in while the list was not refreshed yet: the pointer still gets there.
        _buffer.Configure([Left, Right], EdgeBufferLevel.Strong);

        Assert.Equal(new PhysicalPoint(1000, 1085), Move(0, 1000, 1079, 1000, 1085));
        Assert.False(_buffer.IsPressing);
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

        // Into the gap beside the shorter monitor: Windows keeps the pointer on a monitor itself.
        Assert.Equal(new PhysicalPoint(1925, 100), Move(0, 1919, 100, 1925, 100));
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

    private PhysicalPoint Move(int ms, double fromX, double fromY, double toX, double toY) =>
        _buffer.Filter(EdgeBufferTests.Ticks(ms), new PhysicalPoint(fromX, fromY), new PhysicalPoint(toX, toY));
}
