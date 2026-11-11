using System.Diagnostics;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Tracking;

/// <summary>"Keep the mouse pointer centred" (ADR-0018): where the mouse ends up in the view.</summary>
public sealed class MouseCenteringTests
{
    private static readonly PhysicalRect s_monitor = new(0, 0, 1920, 1080);
    private static readonly TrackingOptions s_centred = TrackingOptions.Default with { PanSeconds = 0, MouseFollow = MouseFollowMode.Center };

    [Theory]
    [InlineData(ZoomMode.Fullscreen)]
    [InlineData(ZoomMode.Lens)]
    public void Mouse_IsShownInTheMiddleOfTheView(ZoomMode mode)
    {
        var mouse = new PhysicalPoint(700, 600);

        var frame = Frame(new AnchorController().Next(Input(1.0, mouse, mode)), mode);

        Assert.Equal(Middle(frame.Target).X, Shown(frame, mouse).X, 6);
        Assert.Equal(Middle(frame.Target).Y, Shown(frame, mouse).Y, 6);
    }

    [Theory]
    [InlineData(ZoomMode.Fullscreen)]
    [InlineData(ZoomMode.Lens)]
    public void ThePointShownUnderThePointer_IsTheMousePoint(ZoomMode mode)
    {
        // The overlay is click-through: a click lands on the real mouse point. It hits what the
        // user sees only if the view shows exactly that point under the drawn pointer.
        // (Docked shows the rest of the monitor in its strip and does not centre: unchanged.)
        var controller = new AnchorController();
        for (var x = 0.0; x < s_monitor.Right; x += 97)
        {
            for (var y = 0.0; y < s_monitor.Bottom; y += 89)
            {
                var mouse = new PhysicalPoint(x, y);
                var frame = Frame(controller.Next(Input(1.0 + (x / 10000) + (y / 1e7), mouse, mode)), mode);

                Assert.True(frame.Source.Contains(mouse), $"{mouse} is outside the view {frame.Source}");
            }
        }
    }

    [Fact]
    public void MovingTheMouse_KeepsThePointerStillAndMovesTheView()
    {
        var controller = new AnchorController();
        var first = Frame(controller.Next(Input(1.0, new PhysicalPoint(800, 500))));
        var second = Frame(controller.Next(Input(1.1, new PhysicalPoint(900, 520))));

        Assert.Equal(Shown(first, new PhysicalPoint(800, 500)).X, Shown(second, new PhysicalPoint(900, 520)).X, 6);
        Assert.Equal(first.Source.X + 100, second.Source.X, 6);
    }

    [Fact]
    public void AtTheMonitorEdge_ViewStops_PointerMovesTowardsTheEdge()
    {
        var mouse = new PhysicalPoint(1915, 1075);

        var frame = Frame(new AnchorController().Next(Input(1.0, mouse)));

        Assert.Equal(s_monitor.Right, frame.Source.Right, 6);
        Assert.Equal(s_monitor.Bottom, frame.Source.Bottom, 6);
        Assert.Equal(1900, Shown(frame, mouse).X, 6);
        Assert.Equal(1060, Shown(frame, mouse).Y, 6);
    }

    [Fact]
    public void ZoomChange_KeepsTheMouseCentred()
    {
        var controller = new AnchorController();
        var mouse = new PhysicalPoint(1000, 400);
        controller.Next(Input(1.0, mouse));

        var frame = Frame(controller.Next(Input(1.1, mouse, zoom: 7)), zoom: 7);

        Assert.Equal(new PhysicalPoint(960, 540), Round(Shown(frame, mouse)));
    }

    [Fact]
    public void Off_ShowsThePointerAtItsDesktopPosition()
    {
        var mouse = new PhysicalPoint(1500, 200);

        var anchor = new AnchorController().Next(Input(1.0, mouse, options: s_centred with { MouseFollow = MouseFollowMode.Fixpoint }));

        Assert.Equal(mouse, anchor);
    }

    [Fact]
    public void BackFromTheFocus_TheMouseIsCentredAgain()
    {
        var controller = new AnchorController();
        controller.Next(Input(1.0, new PhysicalPoint(300, 300)));
        controller.Next(Input(5.0, new PhysicalPoint(300, 300), tracking: new(new PhysicalRect(1500, 800, 100, 30), Ticks(4.9), null, 0)));
        Assert.Equal(AnchorSource.Focus, controller.Source);

        var mouse = new PhysicalPoint(800, 600);
        var frame = Frame(controller.Next(Input(6.0, mouse)));

        Assert.Equal(AnchorSource.Mouse, controller.Source);
        Assert.Equal(new PhysicalPoint(960, 540), Round(Shown(frame, mouse)));
    }

    private static ModeFrame Frame(PhysicalPoint anchor, ZoomMode mode = ZoomMode.Fullscreen, double zoom = 4) =>
        ModeGeometry.Compute(mode, s_monitor, anchor, zoom, LensSize.Default, DockLayout.Default);

    // Where a desktop point appears on screen.
    private static PhysicalPoint Shown(ModeFrame frame, PhysicalPoint p) => new(
        frame.Target.X + ((p.X - frame.Source.X) * frame.Target.Width / frame.Source.Width),
        frame.Target.Y + ((p.Y - frame.Source.Y) * frame.Target.Height / frame.Source.Height));

    private static PhysicalPoint Middle(PhysicalRect r) => new(r.X + (r.Width / 2), r.Y + (r.Height / 2));

    private static PhysicalPoint Round(PhysicalPoint p) => new(Math.Round(p.X, 6), Math.Round(p.Y, 6));

    private static AnchorInput Input(
        double seconds, PhysicalPoint mouse, ZoomMode mode = ZoomMode.Fullscreen, double zoom = 4,
        TrackingSnapshot? tracking = null, TrackingOptions? options = null) =>
        new(Ticks(seconds), mouse, true, s_monitor, mode, zoom, LensSize.Default, DockLayout.Default,
            tracking ?? TrackingSnapshot.Empty, options ?? s_centred);

    private static long Ticks(double seconds) => (long)(seconds * Stopwatch.Frequency);
}
