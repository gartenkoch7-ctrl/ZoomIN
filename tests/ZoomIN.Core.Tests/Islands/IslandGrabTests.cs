using ZoomIN.Core.Islands;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Islands;

/// <summary>Which island a press of the edit mode hits (M19 B1).</summary>
public sealed class IslandGrabTests
{
    private static readonly IslandMonitors s_monitors = new();

    // Lower (0): picture (100,100)–(200,200). Higher (1): picture (150,150)–(250,250).
    private static readonly IslandSet s_overlapping = IslandSet.Empty
        .Add(new Island(0, IslandMonitors.First, new PhysicalRect(0, 0, 50, 50), new PhysicalPoint(100, 100), 2))!
        .Add(new Island(0, IslandMonitors.First, new PhysicalRect(0, 0, 50, 50), new PhysicalPoint(150, 150), 2))!;

    [Fact]
    public void Topmost_UnderThePoint_Wins()
    {
        Assert.Equal(1, Find(new PhysicalPoint(160, 160), out _));
        Assert.Equal(0, Find(new PhysicalPoint(120, 120), out _));
    }

    [Fact]
    public void CornerReachOutsideAPicture_CountsOnlyBesideEveryIsland()
    {
        // 8 px beyond the higher island's bottom-right corner is inside nothing: its corner.
        Assert.Equal(1, Find(new PhysicalPoint(258, 258), out var outside));
        Assert.Equal(IslandCorner.BottomRight, outside);

        // 8 px left of the higher island's top-left corner is inside the lower one: the lower one, moved.
        Assert.Equal(0, Find(new PhysicalPoint(142, 142), out var inside));
        Assert.Null(inside);
        Assert.Equal(-1, Find(new PhysicalPoint(80, 80), out _));
    }

    [Fact]
    public void SmallPicture_CanStillBeMoved_FromItsMiddle()
    {
        var small = IslandSet.Empty.Add(new Island(0, IslandMonitors.First, new PhysicalRect(0, 0, 16, 16), new PhysicalPoint(300, 300), 1))!;

        Assert.Equal(0, IslandGrab.Find(small, s_monitors, new PhysicalPoint(308, 308), withCorners: true, out var corner, out _));
        Assert.Null(corner);
        Assert.Equal(IslandCorner.TopLeft, IslandGrab.CornerAt(small[0].Target, new PhysicalPoint(301, 301)));
    }

    [Fact]
    public void WithoutCorners_OnlyThePictureCounts()
    {
        Assert.Equal(-1, IslandGrab.Find(s_overlapping, s_monitors, new PhysicalPoint(258, 258), withCorners: false, out var corner, out _));
        Assert.Null(corner);
    }

    private static int Find(PhysicalPoint point, out IslandCorner? corner) =>
        IslandGrab.Find(s_overlapping, s_monitors, point, withCorners: true, out corner, out _);
}
