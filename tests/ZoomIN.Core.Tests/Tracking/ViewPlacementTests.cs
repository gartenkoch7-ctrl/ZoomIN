using ZoomIN.Core.Modes;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using static ZoomIN.Core.Tests.Tracking.MouseMarginTests;

namespace ZoomIN.Core.Tests.Tracking;

/// <summary>Placing the mouse view at a point, e.g. "zoom in where the frame is" (M17 B2).</summary>
public sealed class ViewPlacementTests
{
    private static readonly PhysicalPoint s_center = new(1400, 300);

    [Fact]
    public void ZoomingInAfterAPlacement_EndsWithThePointInTheMiddle()
    {
        var controller = new AnchorController();
        controller.Next(Input(1.0, s_center, zoom: 1));
        var placement = new ViewPlacement(s_center, 4, Ticks(1.05));

        PhysicalPoint anchor = default;
        for (var zoom = 1.0; zoom <= 4.0; zoom += 0.25)
        {
            anchor = controller.Next(Input(1.1 + (zoom / 10), s_center, zoom: zoom, placement: placement));
        }

        var shown = Shown(Frame(anchor), s_center);
        Assert.Equal(960, shown.X, 6);
        Assert.Equal(540, shown.Y, 6);
        Assert.Equal(AnchorSource.Mouse, controller.Source);

        // Afterwards the margin rules: the view stays while the pointer is inside it.
        Assert.Equal(anchor, controller.Next(Input(3.0, new PhysicalPoint(1420, 310), placement: placement)));
    }

    [Fact]
    public void WithTheMouseElsewhere_ThePlacementHoldsUntilTheZoomArrives()
    {
        var controller = new AnchorController();
        var mouse = new PhysicalPoint(100, 100);
        controller.Next(Input(1.0, mouse, zoom: 1));
        var placement = new ViewPlacement(s_center, 4, Ticks(1.05));

        PhysicalPoint anchor = default;
        for (var zoom = 1.0; zoom <= 4.0; zoom += 0.5)
        {
            anchor = controller.Next(Input(1.1 + (zoom / 10), mouse, zoom: zoom, placement: placement));
        }

        Assert.Equal(960, Shown(Frame(anchor), s_center).X, 6);

        // Then the margin brings the pointer back into view.
        var frame = Frame(controller.Next(Input(2.0, mouse)));
        Assert.True(frame.Source.Contains(mouse));
    }

    [Fact]
    public void APlacement_TakesTheViewBackFromTheFocus()
    {
        var controller = new AnchorController();
        controller.Next(Input(1.0, new PhysicalPoint(500, 500)));
        controller.Next(Input(5.0, new PhysicalPoint(500, 500), tracking: new(new PhysicalRect(1500, 800, 100, 30), Ticks(4.9), null, 0)));
        Assert.Equal(AnchorSource.Focus, controller.Source);

        var anchor = controller.Next(Input(6.0, new PhysicalPoint(500, 500), placement: new(new PhysicalPoint(600, 500), 4, Ticks(5.9))));

        Assert.Equal(AnchorSource.Mouse, controller.Source);
        Assert.Equal(960, Shown(Frame(anchor), new PhysicalPoint(600, 500)).X, 6);
    }

    [Theory]
    [InlineData(MouseFollowMode.Center, ZoomMode.Fullscreen)]
    [InlineData(MouseFollowMode.Fixpoint, ZoomMode.Fullscreen)]
    [InlineData(MouseFollowMode.Margin, ZoomMode.Docked)]
    public void WithoutTheMargin_APlacementHasNoEffect(MouseFollowMode follow, ZoomMode mode)
    {
        var options = TrackingOptions.Default with { PanSeconds = 0, MouseFollow = follow };
        var mouse = new PhysicalPoint(500, 500);
        var controller = new AnchorController();
        var first = controller.Next(Input(1.0, mouse, mode, options: options));

        var anchor = controller.Next(Input(1.1, mouse, mode, options: options, placement: new(s_center, 4, Ticks(1.05))));

        Assert.Equal(first, anchor);
    }

    [Fact]
    public void APlacementOnAnotherMonitor_IsIgnored()
    {
        var controller = new AnchorController();
        var first = controller.Next(Input(1.0, new PhysicalPoint(500, 500)));

        var anchor = controller.Next(Input(1.1, new PhysicalPoint(500, 500), placement: new(new PhysicalPoint(2500, 500), 4, Ticks(1.05))));

        Assert.Equal(first, anchor);
    }

    [Fact]
    public void APlacementFromBeforeTheStart_IsNoNews()
    {
        var old = new ViewPlacement(s_center, 4, Ticks(0.5));
        var controller = new AnchorController();

        var anchor = controller.Next(Input(1.0, new PhysicalPoint(500, 500), placement: old));

        Assert.Equal(new PhysicalPoint(500, 500), anchor);
    }

    [Fact]
    public void AZoomThatStopsShort_EndsThePlacement()
    {
        var controller = new AnchorController();
        controller.Next(Input(1.0, s_center, zoom: 1));
        controller.Next(Input(1.1, s_center, zoom: 1, placement: new(s_center, 4, Ticks(1.05))));
        controller.Next(Input(1.2, s_center, zoom: 2));
        controller.Next(Input(1.3, s_center, zoom: 3));
        controller.Next(Input(1.4, s_center, zoom: 3)); // one frame without a step: still held
        controller.Next(Input(1.8, s_center, zoom: 3)); // stopped at 3 (e.g. clamped)

        // A later zoom change keeps the pointer in place again.
        var before = Shown(Frame(controller.Next(Input(1.9, s_center, zoom: 3)), zoom: 3), s_center);
        var after = Shown(Frame(controller.Next(Input(2.0, s_center, zoom: 3.5)), zoom: 3.5), s_center);

        Assert.Equal(before.X, after.X, 6);
        Assert.Equal(before.Y, after.Y, 6);
    }

    [Fact]
    public void AZoomThatNeverStarts_DoesNotFreezeTheView()
    {
        var controller = new AnchorController();
        var mouse = new PhysicalPoint(100, 100);
        controller.Next(Input(1.0, mouse));

        // Placed for 20× while the zoom stays at 4× (e.g. already at the limit).
        var placed = controller.Next(Input(1.1, mouse, placement: new(s_center, 20, Ticks(1.05))));
        Assert.False(Frame(placed).Source.Contains(mouse));
        Assert.Equal(placed, controller.Next(Input(1.2, mouse)));

        controller.Next(Input(1.5, mouse));
        var frame = Frame(controller.Next(Input(1.6, mouse)));
        Assert.True(frame.Source.Contains(mouse), "after a moment the margin brings the pointer back");
    }

    [Fact]
    public void AZoomChangeAwayFromThePlacement_EndsIt()
    {
        var controller = new AnchorController();
        controller.Next(Input(1.0, s_center, zoom: 1));
        controller.Next(Input(1.1, s_center, zoom: 2, placement: new(s_center, 4, Ticks(1.05))));

        // The user zooms out again before arriving: the pointer keeps its place on screen again.
        var before = Shown(Frame(controller.Next(Input(1.2, s_center, zoom: 1.5)), zoom: 1.5), s_center);
        var after = Shown(Frame(controller.Next(Input(1.3, s_center, zoom: 1.4)), zoom: 1.4), s_center);

        Assert.Equal(before.X, after.X, 6);
        Assert.Equal(before.Y, after.Y, 6);
    }
}
