using System.Diagnostics;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Tracking;

/// <summary>Mouse follow with margin (M17 A1): the view stands still until the pointer reaches the margin.</summary>
public sealed class MouseMarginTests
{
    private static readonly PhysicalRect s_monitor = new(0, 0, 1920, 1080);
    private static readonly PhysicalPoint s_start = new(960, 540);
    private static readonly TrackingOptions s_margin = TrackingOptions.Default with { PanSeconds = 0 };

    [Fact]
    public void Margin_IsTheFactoryDefault()
    {
        Assert.Equal(MouseFollowMode.Margin, TrackingOptions.Default.MouseFollow);
        Assert.Equal(0.2, TrackingOptions.Default.MouseMargin);
    }

    [Fact]
    public void InsideTheMargin_TheViewStandsStill()
    {
        var controller = new AnchorController();
        var first = controller.Next(Input(1.0, s_start));

        for (var i = 1; i <= 20; i++)
        {
            // ±100 px around the start stays well inside the inner 60 % of a 480 × 270 view.
            var mouse = new PhysicalPoint(s_start.X + (i % 2 == 0 ? 100 : -100), s_start.Y + (i % 3 == 0 ? 60 : -60));
            Assert.Equal(first, controller.Next(Input(1.0 + (i / 60.0), mouse)));
        }
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, 1)]
    [InlineData(0, -1)]
    public void BeyondTheMargin_TheViewIsPushed_AndThePointerStaysOnTheMargin(int dx, int dy)
    {
        var controller = new AnchorController();
        controller.Next(Input(1.0, s_start));
        ModeFrame previous = default;
        for (var step = 1; step <= 30; step++)
        {
            var mouse = new PhysicalPoint(s_start.X + (dx * step * 10), s_start.Y + (dy * step * 6));
            var frame = Frame(controller.Next(Input(1.0 + (step / 60.0), mouse)));
            var shown = Shown(frame, mouse);

            // The pointer never gets closer to the edge than the margin (20 % of the monitor) …
            Assert.InRange(shown.X, (1920 * 0.2) - 1e-6, (1920 * 0.8) + 1e-6);
            Assert.InRange(shown.Y, (1080 * 0.2) - 1e-6, (1080 * 0.8) + 1e-6);

            // … and once it pushes, the view moves exactly with the mouse: no jump, no delay.
            if (step > 20)
            {
                Assert.Equal(previous.Source.X + (dx * 10), frame.Source.X, 6);
                Assert.Equal(previous.Source.Y + (dy * 6), frame.Source.Y, 6);
            }

            previous = frame;
        }

        var edge = Shown(previous, new PhysicalPoint(s_start.X + (dx * 300), s_start.Y + (dy * 180)));
        Assert.Equal(dx switch { 1 => 1536, -1 => 384, _ => edge.X }, edge.X, 6);
        Assert.Equal(dy switch { 1 => 864, -1 => 216, _ => edge.Y }, edge.Y, 6);
    }

    [Fact]
    public void AtTheMonitorEdge_TheViewStops_WithoutJitter()
    {
        var controller = new AnchorController();
        controller.Next(Input(1.0, s_start));
        for (var x = 960.0; x <= 1915; x += 5)
        {
            controller.Next(Input(1.0 + (x / 10000), new PhysicalPoint(x, 540)));
        }

        var anchor = controller.Next(Input(2.0, new PhysicalPoint(1915, 540)));
        for (var i = 1; i <= 30; i++)
        {
            // Same mouse point, also after tiny vertical moves inside the margin: bit-identical anchor X.
            Assert.Equal(anchor.X, controller.Next(Input(2.0 + (i / 60.0), new PhysicalPoint(1915, 540 + (i % 2)))).X);
        }

        var frame = Frame(anchor);
        Assert.Equal(s_monitor.Right, frame.Source.Right, 6);
        Assert.True(Shown(frame, new PhysicalPoint(1915, 540)).X > 1536, "the pointer moves on into the margin");
    }

    [Fact]
    public void ZoomChange_KeepsThePointerWhereItIsOnScreen()
    {
        var controller = new AnchorController();
        controller.Next(Input(1.0, s_start));
        var mouse = new PhysicalPoint(1060, 580);
        var before = Shown(Frame(controller.Next(Input(1.1, mouse))), mouse);

        var zoom = 4.0;
        for (var i = 1; i <= 20; i++)
        {
            zoom += 0.1;
            var frame = Frame(controller.Next(Input(1.1 + (i / 60.0), mouse, zoom: zoom)), zoom: zoom);
            Assert.Equal(before.X, Shown(frame, mouse).X, 6);
            Assert.Equal(before.Y, Shown(frame, mouse).Y, 6);
        }
    }

    [Fact]
    public void ZoomingInFromOne_StartsAtThePointer()
    {
        var controller = new AnchorController();
        controller.Next(Input(1.0, s_start, zoom: 1));
        var mouse = new PhysicalPoint(1500, 300);
        controller.Next(Input(1.1, mouse, zoom: 1));

        var anchor = controller.Next(Input(1.2, mouse, zoom: 1.2));

        // Shown at its own desktop position, like the fixpoint (ADR-0003).
        Assert.Equal(mouse.X, Shown(Frame(anchor, zoom: 1.2), mouse).X, 6);
        Assert.Equal(mouse.Y, Shown(Frame(anchor, zoom: 1.2), mouse).Y, 6);
    }

    [Fact]
    public void BackFromTheFocus_TheViewStaysWhereTheKeyboardLeftIt()
    {
        var controller = new AnchorController();
        controller.Next(Input(1.0, s_start));
        var focus = new PhysicalRect(1300, 700, 100, 30);
        var atFocus = controller.Next(Input(5.0, s_start, tracking: new(focus, Ticks(4.9), null, 0)));
        Assert.Equal(AnchorSource.Focus, controller.Source);

        // The mouse comes back into the middle of the focus view: the view stays where it is.
        var view = Frame(atFocus).Source;
        var inside = new PhysicalPoint(view.X + (view.Width / 2) - 20, view.Y + (view.Height / 2));
        var back = controller.Next(Input(6.0, inside));

        Assert.Equal(AnchorSource.Mouse, controller.Source);
        Assert.Equal(atFocus, back);
    }

    [Fact]
    public void Lens_StandsStillInsideTheMargin_AndPushesBeyond()
    {
        var controller = new AnchorController();
        var first = controller.Next(Input(1.0, s_start, ZoomMode.Lens));

        // The lens shows 192 × 81 desktop pixels at 4×; +30/−20 px stays inside its inner 60 %.
        Assert.Equal(first, controller.Next(Input(1.1, new PhysicalPoint(990, 520), ZoomMode.Lens)));

        var mouse = new PhysicalPoint(1100, 540);
        var frame = Frame(controller.Next(Input(1.2, mouse, ZoomMode.Lens)), ZoomMode.Lens);
        Assert.Equal(frame.Target.X + (frame.Target.Width * 0.8), Shown(frame, mouse).X, 6);
    }

    [Fact]
    public void SwitchingToTheLens_OpensItAtThePointer()
    {
        var controller = new AnchorController();
        controller.Next(Input(1.0, s_start));
        var mouse = new PhysicalPoint(1060, 600);
        Assert.Equal(s_start, controller.Next(Input(1.1, mouse))); // inside the margin: no move

        var frame = Frame(controller.Next(Input(1.2, mouse, ZoomMode.Lens)), ZoomMode.Lens);

        Assert.Equal(frame.Target.X + (frame.Target.Width / 2), Shown(frame, mouse).X, 6);
        Assert.Equal(frame.Target.Y + (frame.Target.Height / 2), Shown(frame, mouse).Y, 6);
    }

    [Fact]
    public void SwitchingToTheLens_WhileTheMouseIsElsewhere_OpensItAtThePointerOnReturn()
    {
        var controller = new AnchorController();
        controller.Next(Input(1.0, s_start));
        controller.Next(Input(1.1, new PhysicalPoint(2500, 500), ZoomMode.Lens) with { MouseInside = false });
        var mouse = new PhysicalPoint(1060, 600);

        var frame = Frame(controller.Next(Input(1.2, mouse, ZoomMode.Lens)), ZoomMode.Lens);

        Assert.Equal(frame.Target.X + (frame.Target.Width / 2), Shown(frame, mouse).X, 6);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0.7)]
    public void HalfOrMoreMargin_KeepsThePointerInTheMiddle(double margin)
    {
        var controller = new AnchorController();
        controller.Next(Input(1.0, s_start, options: s_margin with { MouseMargin = margin }));
        var mouse = new PhysicalPoint(700, 600);

        var frame = Frame(controller.Next(Input(1.1, mouse, options: s_margin with { MouseMargin = margin })));

        Assert.Equal(960, Shown(frame, mouse).X, 6);
        Assert.Equal(540, Shown(frame, mouse).Y, 6);
    }

    [Fact]
    public void ZeroMargin_PushesOnlyAtTheVeryEdge()
    {
        var options = s_margin with { MouseMargin = 0 };
        var controller = new AnchorController();
        var first = controller.Next(Input(1.0, s_start, options: options));
        var view = Frame(first).Source;

        Assert.Equal(first, controller.Next(Input(1.1, new PhysicalPoint(view.Right - 1, s_start.Y), options: options)));

        var frame = Frame(controller.Next(Input(1.2, new PhysicalPoint(view.Right + 10, s_start.Y), options: options)));
        Assert.Equal(1920, Shown(frame, new PhysicalPoint(view.Right + 10, s_start.Y)).X, 6);
    }

    internal static ModeFrame Frame(PhysicalPoint anchor, ZoomMode mode = ZoomMode.Fullscreen, double zoom = 4) =>
        ModeGeometry.Compute(mode, s_monitor, anchor, zoom, LensSize.Default, DockLayout.Default);

    // Where a desktop point appears on screen.
    internal static PhysicalPoint Shown(ModeFrame frame, PhysicalPoint p) => new(
        frame.Target.X + ((p.X - frame.Source.X) * frame.Target.Width / frame.Source.Width),
        frame.Target.Y + ((p.Y - frame.Source.Y) * frame.Target.Height / frame.Source.Height));

    internal static AnchorInput Input(
        double seconds, PhysicalPoint mouse, ZoomMode mode = ZoomMode.Fullscreen, double zoom = 4,
        TrackingSnapshot? tracking = null, TrackingOptions? options = null, ViewPlacement placement = default) =>
        new(Ticks(seconds), mouse, true, s_monitor, mode, zoom, LensSize.Default, DockLayout.Default,
            tracking ?? TrackingSnapshot.Empty, options ?? s_margin, placement);

    internal static long Ticks(double seconds) => (long)(seconds * Stopwatch.Frequency);
}
