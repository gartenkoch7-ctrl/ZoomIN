using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Pointer;

public sealed class PointerPlacementTests
{
    private static readonly PhysicalRect s_monitor = new(0, 0, 1920, 1080);

    // A big arrow (4× zoom): 128 px box, tip at the top left (inside a 4 px rim).
    private const double Size = 128;
    private const double Hotspot = 4;

    [Fact]
    public void InTheMiddle_DrawsNormallyWithTheTipOnTheClickPoint()
    {
        var placement = Place(new PhysicalPoint(900, 500));

        Assert.Equal(new PointerPlacement(896, 496, false, false), placement);
    }

    [Fact]
    public void AtTheBottomEdge_FlipsUp_TipStaysOnTheClickPoint()
    {
        var placement = Place(new PhysicalPoint(900, 1079));

        Assert.True(placement.FlipY);
        Assert.False(placement.FlipX);
        Assert.Equal(1079 - (Size - Hotspot), placement.Y);
        Assert.True(placement.Y + Size <= s_monitor.Bottom + Hotspot, "the image must be on screen");
    }

    [Fact]
    public void AtTheRightEdge_FlipsLeft()
    {
        var placement = Place(new PhysicalPoint(1919, 500));

        Assert.True(placement.FlipX);
        Assert.False(placement.FlipY);
        Assert.Equal(1919 - (Size - Hotspot), placement.X);
    }

    [Fact]
    public void InTheBottomRightCorner_FlipsBoth()
    {
        var placement = Place(new PhysicalPoint(1915, 1075));

        Assert.True(placement.FlipX && placement.FlipY);
    }

    [Fact]
    public void AtTheTopLeftEdge_DoesNotFlip()
    {
        Assert.Equal(new PointerPlacement(-4, -4, false, false), Place(new PhysicalPoint(0, 0)));
    }

    [Fact]
    public void SlightlyCut_DoesNotFlipYet()
    {
        // 10 px of 128 cut off (below an eighth): not worth turning the arrow around.
        var placement = Place(new PhysicalPoint(900, 1080 - Size + Hotspot + 10));

        Assert.False(placement.FlipY);
    }

    [Fact]
    public void RestingOnTheBoundary_DoesNotFlicker()
    {
        // Flips at 16 px cut (an eighth); stays flipped until less than 8 px would be cut.
        var flipped = Place(new PhysicalPoint(900, 1080 - Size + Hotspot + 20));
        Assert.True(flipped.FlipY);

        var back = PointerPlacement.Place(
            new PhysicalPoint(900, 1080 - Size + Hotspot + 12), Size, Size, Hotspot, Hotspot, s_monitor, flipped);
        Assert.True(back.FlipY);

        var away = PointerPlacement.Place(
            new PhysicalPoint(900, 1080 - Size + Hotspot + 4), Size, Size, Hotspot, Hotspot, s_monitor, back);
        Assert.False(away.FlipY);
    }

    [Fact]
    public void CentredHotspot_NeverFlips()
    {
        // I-beam: mirroring would not show more of it.
        var placement = PointerPlacement.Place(new PhysicalPoint(1919, 1079), 64, 64, 32, 32, s_monitor, default);

        Assert.False(placement.FlipX || placement.FlipY);
    }

    [Fact]
    public void SecondMonitorWithNegativeOrigin_UsesItsOwnEdges()
    {
        var left = new PhysicalRect(-1920, 0, 1920, 1080);

        var placement = PointerPlacement.Place(new PhysicalPoint(-1, 500), Size, Size, Hotspot, Hotspot, left, default);

        Assert.True(placement.FlipX);
    }

    private static PointerPlacement Place(PhysicalPoint tip) =>
        PointerPlacement.Place(tip, Size, Size, Hotspot, Hotspot, s_monitor, default);
}
