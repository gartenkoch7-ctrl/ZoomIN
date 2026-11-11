using System.Diagnostics;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Tracking;

public sealed class AnchorControllerTests
{
    private static readonly PhysicalRect s_monitor = new(0, 0, 1920, 1080);
    private static readonly PhysicalPoint s_mouse = new(300, 300);
    // Mouse as fixpoint (ADR-0003): the view starts exactly around s_mouse. Centring: MouseCenteringTests.
    private static readonly TrackingOptions s_instant = TrackingOptions.Default with { PanSeconds = 0, MouseFollow = MouseFollowMode.Fixpoint };

    [Fact]
    public void WithoutKeyboardEvents_FollowsMouseExactly()
    {
        var controller = new AnchorController();

        var anchor = controller.Next(Input(1.0, s_mouse));

        Assert.Equal(s_mouse, anchor);
        Assert.Equal(AnchorSource.Mouse, controller.Source);
    }

    [Fact]
    public void FocusNearTheMonitorEdge_ViewStopsAtTheEdge()
    {
        var controller = Started();
        var focus = new PhysicalRect(1700, 900, 100, 40);

        var anchor = controller.Next(Input(5.0, s_mouse, Focus(focus, 4.9)));

        var view = ModeGeometry.Compute(ZoomMode.Fullscreen, s_monitor, anchor, 4, LensSize.Default, DockLayout.Default).Source;
        Assert.Equal(s_monitor.Right, view.Right, 6);
        Assert.Equal(920, view.Y + (view.Height / 2), 6); // vertically there is room: centred
        Assert.Equal(AnchorSource.Focus, controller.Source);
    }

    [Fact]
    public void FocusRightAfterMouseMovement_IsIgnored()
    {
        var controller = Started();
        controller.Next(Input(4.8, new PhysicalPoint(320, 300))); // mouse moved, e.g. to click

        var anchor = controller.Next(Input(5.0, new PhysicalPoint(320, 300), Focus(new PhysicalRect(1700, 900, 100, 40), 4.9)));

        Assert.Equal(new PhysicalPoint(320, 300), anchor);
        Assert.Equal(AnchorSource.Mouse, controller.Source);
    }

    [Fact]
    public void Caret_MovesOnlyWhenNearTheEdge()
    {
        var controller = Started();
        var view = ModeGeometry.Compute(ZoomMode.Fullscreen, s_monitor, s_mouse, 4, LensSize.Default, DockLayout.Default).Source;

        var inCenter = new PhysicalRect(view.X + (view.Width / 2), view.Y + (view.Height / 2), 2, 20);
        Assert.Equal(s_mouse, controller.Next(Input(5.0, s_mouse, Caret(inCenter, 4.9))));

        var nearEdge = new PhysicalRect(view.Right - 5, view.Y + (view.Height / 2), 2, 20);
        Assert.NotEqual(s_mouse, controller.Next(Input(5.1, s_mouse, Caret(nearEdge, 5.05))));
        Assert.Equal(AnchorSource.Caret, controller.Source);
    }

    [Fact]
    public void MouseMovement_TakesTheViewBack()
    {
        var controller = Started();
        controller.Next(Input(5.0, s_mouse, Focus(new PhysicalRect(1700, 900, 100, 40), 4.9)));

        var moved = new PhysicalPoint(330, 300);
        var anchor = controller.Next(Input(6.0, moved, Focus(new PhysicalRect(1700, 900, 100, 40), 4.9)));

        Assert.Equal(moved, anchor);
        Assert.Equal(AnchorSource.Mouse, controller.Source);
    }

    [Fact]
    public void Glide_ApproachesTargetGradually()
    {
        var controller = Started(TrackingOptions.Default);
        var focus = new PhysicalRect(1700, 900, 100, 40);

        var first = controller.Next(Input(5.0, s_mouse, Focus(focus, 4.9), TrackingOptions.Default));
        var later = first;
        for (var frame = 1; frame <= 30; frame++)
        {
            later = controller.Next(Input(5.0 + (frame / 60.0), s_mouse, Focus(focus, 4.9), TrackingOptions.Default));
        }

        // Centring (1750, 920) stops at the corner: the fixpoint is the corner itself.
        Assert.True(first.X < s_monitor.Right && first.X > s_mouse.X, $"first frame should be on the way, got {first}");
        Assert.Equal(s_monitor.Right, later.X, 0);
    }

    [Fact]
    public void Disabled_IgnoresFocus()
    {
        var controller = Started();
        var options = s_instant with { Enabled = false };

        var anchor = controller.Next(Input(5.0, s_mouse, Focus(new PhysicalRect(1700, 900, 100, 40), 4.9), options));

        Assert.Equal(s_mouse, anchor);
    }

    [Fact]
    public void HugeFocus_IsIgnored()
    {
        var controller = Started();

        var anchor = controller.Next(Input(5.0, s_mouse, Focus(new PhysicalRect(0, 0, 1900, 1000), 4.9)));

        Assert.Equal(s_mouse, anchor);
    }

    [Fact]
    public void MouseOnOtherMonitor_KeepsLastView()
    {
        var controller = Started();

        var anchor = controller.Next(Input(5.0, new PhysicalPoint(2500, 300), mouseInside: false));

        Assert.Equal(s_mouse, anchor);
    }

    [Fact]
    public void EstimatedCaret_GridCellIsPushedLikeACaret_NotCentredLikeAFocus()
    {
        var controller = Started();
        var view = ModeGeometry.Compute(ZoomMode.Fullscreen, s_monitor, s_mouse, 4, LensSize.Default, DockLayout.Default).Source;
        var cell = new PhysicalRect(view.Right - 20, view.Y + (view.Height / 2), 16, 8);
        var tracking = new TrackingSnapshot(cell, Ticks(4.9), cell, Ticks(4.9), CaretEstimated: true);

        var anchor = controller.Next(Input(5.0, s_mouse, tracking));

        var moved = ModeGeometry.Compute(ZoomMode.Fullscreen, s_monitor, anchor, 4, LensSize.Default, DockLayout.Default).Source;
        Assert.Equal(AnchorSource.Caret, controller.Source);
        Assert.Equal(moved.Right - (moved.Width * s_instant.CaretMargin), cell.Right, 6); // on the margin
    }

    private static AnchorController Started(TrackingOptions? options = null)
    {
        var controller = new AnchorController();
        controller.Next(Input(1.0, s_mouse, options: options ?? s_instant));
        return controller;
    }

    private static TrackingSnapshot Focus(PhysicalRect rect, double at) => new(rect, Ticks(at), null, 0);

    private static TrackingSnapshot Caret(PhysicalRect rect, double at) => new(null, 0, rect, Ticks(at));

    private static AnchorInput Input(
        double seconds, PhysicalPoint mouse, TrackingSnapshot? tracking = null, TrackingOptions? options = null, bool mouseInside = true) =>
        new(Ticks(seconds), mouse, mouseInside, s_monitor, ZoomMode.Fullscreen, 4, LensSize.Default, DockLayout.Default,
            tracking ?? TrackingSnapshot.Empty, options ?? s_instant);

    private static long Ticks(double seconds) => (long)(seconds * Stopwatch.Frequency);
}
