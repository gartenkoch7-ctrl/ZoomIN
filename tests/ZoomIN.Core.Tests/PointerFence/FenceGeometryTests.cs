using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.PointerFence;

public sealed class FenceGeometryTests
{
    // Layout of the M19 demo: 1 left, 3 middle, 2 right, all 1920×1080.
    private static readonly PhysicalRect Left = new(0, 0, 1920, 1080);
    private static readonly PhysicalRect Middle = new(1920, 0, 1920, 1080);
    private static readonly PhysicalRect Right = new(3840, 0, 1920, 1080);
    private static readonly PhysicalRect[] Three = [Left, Middle, Right];

    [Fact]
    public void NothingForbidden_IsUnrestricted_AndOneRectForEqualMonitors()
    {
        var geometry = FenceGeometry.Create(Three, []);

        Assert.False(geometry.IsRestricted);
        Assert.False(geometry.IsEmpty);
        Assert.Equal(new PhysicalRect(0, 0, 5760, 1080), geometry.SingleRect);
    }

    [Fact]
    public void ForbiddenRightMonitor_LeavesOneRectangle_ForClipCursor()
    {
        var geometry = FenceGeometry.Create(Three, [Right]);

        Assert.True(geometry.IsRestricted);
        Assert.Equal(new PhysicalRect(0, 0, 3840, 1080), geometry.SingleRect);
        Assert.False(geometry.IsAllowed(new PhysicalPoint(4000, 10)));
        Assert.True(geometry.IsAllowed(new PhysicalPoint(3839, 1079)));
    }

    [Fact]
    public void ForbiddenMiddleMonitor_SplitsTheArea_AndNeedsTheHook()
    {
        var geometry = FenceGeometry.Create(Three, [Middle]);

        Assert.Null(geometry.SingleRect);
        Assert.Equal(2, geometry.Pieces.Count);
    }

    [Fact]
    public void MonitorsOfDifferentHeight_AreNotOneRectangle()
    {
        var geometry = FenceGeometry.Create([Left, new PhysicalRect(1920, 0, 2560, 1440), Right], [Right]);

        Assert.Null(geometry.SingleRect);
    }

    [Fact]
    public void AllMonitorsForbidden_IsEmpty()
    {
        var geometry = FenceGeometry.Create(Three, Three);

        Assert.True(geometry.IsEmpty);
        Assert.Null(geometry.SingleRect);
        Assert.False(geometry.IsAllowed(new PhysicalPoint(10, 10)));
        Assert.Equal(new PhysicalPoint(10, 10), geometry.Nearest(new PhysicalPoint(10, 10)));
    }

    [Fact]
    public void NoMonitors_IsEmpty()
    {
        Assert.True(FenceGeometry.Create([], [Left]).IsEmpty);
    }

    [Fact]
    public void ForbiddenRectOutsideAllMonitors_RestrictsNothing()
    {
        var geometry = FenceGeometry.Create(Three, [new PhysicalRect(-500, -500, 100, 100), new PhysicalRect(0, 0, 0, 50)]);

        Assert.False(geometry.IsRestricted);
    }

    [Fact]
    public void MirroredMonitors_CountOnce()
    {
        var geometry = FenceGeometry.Create([Left, Left, new PhysicalRect(0, 0, 1280, 720)], []);

        Assert.Single(geometry.Pieces);
        Assert.Equal(Left, geometry.SingleRect);
    }

    [Fact]
    public void HoleInsideAMonitor_LeavesFourDisjointPieces()
    {
        var hole = new PhysicalRect(100, 100, 200, 200);
        var geometry = FenceGeometry.Create([Left], [hole]);

        Assert.Equal(4, geometry.Pieces.Count);
        Assert.Null(geometry.SingleRect);
        var area = geometry.Pieces.Sum(piece => piece.Width * piece.Height);
        Assert.Equal((1920 * 1080) - (200 * 200), area);
        Assert.False(geometry.IsAllowed(new PhysicalPoint(150, 150)));
        Assert.True(geometry.IsAllowed(new PhysicalPoint(99, 150)));
    }

    [Fact]
    public void CutAtTheEdge_KeepsOneRectangle()
    {
        var geometry = FenceGeometry.Create([Left], [new PhysicalRect(0, 0, 1920, 40)]);

        Assert.Equal(new PhysicalRect(0, 40, 1920, 1040), geometry.SingleRect);
    }

    [Fact]
    public void Constrain_AllowedMove_PassesUnchanged()
    {
        var geometry = FenceGeometry.Create(Three, [Right]);

        Assert.Equal(new PhysicalPoint(2000, 500), geometry.Constrain(new PhysicalPoint(1900, 500), new PhysicalPoint(2000, 500)));
    }

    [Fact]
    public void Constrain_IntoForbiddenMonitor_StopsAtTheLastPixel()
    {
        var geometry = FenceGeometry.Create(Three, [Middle]);

        var allowed = geometry.Constrain(new PhysicalPoint(1910, 500), new PhysicalPoint(1935, 520));

        Assert.Equal(new PhysicalPoint(1919, 520), allowed);
    }

    [Fact]
    public void Constrain_NeverJumpsOverAForbiddenMonitor()
    {
        var geometry = FenceGeometry.Create(Three, [Middle]);

        // Nearer to monitor 2 than to monitor 1, but the pointer comes from 1.
        var allowed = geometry.Constrain(new PhysicalPoint(1915, 500), new PhysicalPoint(3830, 500));

        Assert.Equal(new PhysicalPoint(1919, 500), allowed);
    }

    [Fact]
    public void Constrain_PointerAlreadyInsideForbiddenArea_MovesFreely()
    {
        var geometry = FenceGeometry.Create(Three, [Middle]);

        var requested = new PhysicalPoint(2500, 300);

        Assert.Equal(requested, geometry.Constrain(new PhysicalPoint(2490, 300), requested));
    }

    [Fact]
    public void Constrain_AroundAHole_StaysInTheCurrentPiece()
    {
        var geometry = FenceGeometry.Create([Left], [new PhysicalRect(100, 100, 200, 200)]);

        Assert.Equal(new PhysicalPoint(99, 150), geometry.Constrain(new PhysicalPoint(95, 150), new PhysicalPoint(120, 150)));
        Assert.Equal(new PhysicalPoint(150, 99), geometry.Constrain(new PhysicalPoint(150, 90), new PhysicalPoint(150, 120)));
    }

    [Fact]
    public void Constrain_SlidesIntoATouchingPiece_InsteadOfStickingAtASeam()
    {
        var geometry = FenceGeometry.Create([Left], [new PhysicalRect(100, 100, 200, 200)]);

        // From the band above the hole, down into the hole near its left edge: the strip left of it continues.
        Assert.Equal(new PhysicalPoint(99, 150), geometry.Constrain(new PhysicalPoint(95, 95), new PhysicalPoint(105, 150)));
    }

    [Fact]
    public void Constrain_NeverSlipsOverACornerOntoAThirdMonitor()
    {
        var below = new PhysicalRect(1920, 1080, 1920, 1080);
        var geometry = FenceGeometry.Create([Left, Middle, below], [Middle]);

        var allowed = geometry.Constrain(new PhysicalPoint(1919, 1070), new PhysicalPoint(1930, 1072));

        Assert.Equal(new PhysicalPoint(1919, 1072), allowed);
    }

    [Fact]
    public void FractionalForbiddenRect_IsRoundedOutwards_SoClampsLandOnAllowedPixels()
    {
        var geometry = FenceGeometry.Create([Left], [new PhysicalRect(0, 0, 100.5, 1080)]);

        var allowed = geometry.Constrain(new PhysicalPoint(200, 10), new PhysicalPoint(99, 10));

        Assert.Equal(new PhysicalPoint(101, 10), allowed);
        Assert.False(geometry.IsAllowed(new PhysicalPoint(100, 10)));
    }

    [Fact]
    public void Nearest_PicksTheClosestAllowedPixel()
    {
        var geometry = FenceGeometry.Create(Three, [Middle]);

        Assert.Equal(new PhysicalPoint(1919, 400), geometry.Nearest(new PhysicalPoint(2100, 400)));
        Assert.Equal(new PhysicalPoint(3840, 400), geometry.Nearest(new PhysicalPoint(3700, 400)));
        Assert.Equal(new PhysicalPoint(10, 10), geometry.Nearest(new PhysicalPoint(10, 10)));
    }

    [Fact]
    public void Create_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => FenceGeometry.Create(null!, []));
        Assert.Throws<ArgumentNullException>(() => FenceGeometry.Create([], null!));
    }
}
