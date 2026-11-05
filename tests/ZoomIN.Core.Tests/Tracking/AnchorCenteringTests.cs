using System.Diagnostics;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Tracking;

/// <summary>Where focus and caret end up in the magnified view (centred, pushed, clamped).</summary>
public sealed class AnchorCenteringTests
{
    private static readonly PhysicalRect s_monitor = new(0, 0, 1920, 1080);
    private static readonly PhysicalPoint s_mouse = new(300, 300);
    // Mouse as fixpoint (ADR-0003): the view starts exactly around s_mouse. Centring: MouseCenteringTests.
    private static readonly TrackingOptions s_instant = TrackingOptions.Default with { PanSeconds = 0, CenterMouse = false };

    [Theory]
    [InlineData(ZoomMode.Fullscreen)]
    [InlineData(ZoomMode.Lens)]
    [InlineData(ZoomMode.Docked)]
    public void Focus_IsShownInTheMiddleOfTheView(ZoomMode mode)
    {
        var controller = Started(mode);
        var focus = new PhysicalRect(700, 600, 120, 30);

        var view = View(controller.Next(Input(5.0, Focus(focus, 4.9), mode)), mode);

        Assert.Equal(760, Middle(view).X, 6);
        Assert.Equal(615, Middle(view).Y, 6);
    }

    [Fact]
    public void FocusAlreadyVisibleNearTheEdge_IsCentredAnyway()
    {
        var controller = Started();
        var start = View(s_mouse);
        var focus = new PhysicalRect(start.Right - 60, start.Y + 10, 50, 20);

        var view = View(controller.Next(Input(5.0, Focus(focus, 4.9))));

        Assert.Equal(new PhysicalPoint(focus.X + 25, focus.Y + 10), Middle(view));
    }

    [Fact]
    public void FocusOnSecondMonitorWithNegativeOrigin_IsCentred()
    {
        var left = new PhysicalRect(-1920, 0, 1920, 1080);
        var controller = new AnchorController();
        controller.Next(Input(1.0, null, monitor: left, mouseInside: false));
        var focus = new PhysicalRect(-1000, 500, 100, 40);

        var anchor = controller.Next(Input(5.0, Focus(focus, 4.9), monitor: left, mouseInside: false));

        Assert.Equal(new PhysicalPoint(-950, 520), Middle(View(anchor, monitor: left)));
    }

    [Fact]
    public void ZoomChange_KeepsTheFocusCentred()
    {
        var controller = Started();
        var focus = Focus(new PhysicalRect(1000, 500, 100, 40), 4.9);
        controller.Next(Input(5.0, focus));

        var anchor = controller.Next(Input(5.1, focus, zoom: 7));

        Assert.Equal(new PhysicalPoint(1050, 520), Middle(View(anchor, zoom: 7)));
    }

    [Fact]
    public void CaretInTheMargin_IsPushedToTheMarginNotToTheMiddle()
    {
        var controller = Started();
        var start = View(s_mouse);
        var caret = new PhysicalRect(start.Right - 30, start.Y + 100, 2, 20);

        var view = View(controller.Next(Input(5.0, Caret(caret, 4.9))));

        Assert.Equal(view.Right - (view.Width * 0.2), caret.Right, 6);
        Assert.Equal(start.Y, view.Y, 6);
    }

    [Fact]
    public void Typing_ScrollsTheViewEvenly_WithoutJumpingBack()
    {
        var controller = Started();
        var start = View(s_mouse);
        var x = start.X + (start.Width / 2);
        var lastLeft = start.X;
        for (var i = 1; i <= 40; i++)
        {
            x += 9; // one character
            var view = View(controller.Next(Input(5.0 + (i * 0.1), Caret(new PhysicalRect(x, start.Y + 100, 2, 20), 4.95 + (i * 0.1)))));

            Assert.InRange(view.X - lastLeft, 0, 9.000001); // never backwards, never more than a character
            Assert.InRange(x + 2, view.X, view.Right - (view.Width * 0.2) + 1e-6);
            lastLeft = view.X;
        }

        Assert.True(lastLeft > start.X + 100, "the view should have followed the caret");
    }

    [Fact]
    public void CaretOutOfSight_IsCentred()
    {
        var controller = Started();
        var caret = new PhysicalRect(1200, 700, 2, 20);

        var view = View(controller.Next(Input(5.0, Caret(caret, 4.9))));

        Assert.Equal(new PhysicalPoint(1201, 710), Middle(view));
    }

    [Fact]
    public void CaretNearTheMonitorEdge_StopsAtTheEdgeWithoutJitter()
    {
        var controller = Started();
        controller.Next(Input(5.0, Caret(new PhysicalRect(1910, 500, 2, 20), 4.9)));

        var first = controller.Next(Input(5.1, Caret(new PhysicalRect(1912, 500, 2, 20), 5.05)));
        var second = controller.Next(Input(5.2, Caret(new PhysicalRect(1914, 500, 2, 20), 5.15)));

        Assert.Equal(s_monitor.Right, View(first).Right, 6);
        Assert.Equal(first, second);
    }

    private static AnchorController Started(ZoomMode mode = ZoomMode.Fullscreen)
    {
        var controller = new AnchorController();
        controller.Next(Input(1.0, null, mode));
        return controller;
    }

    private static PhysicalRect View(PhysicalPoint anchor, ZoomMode mode = ZoomMode.Fullscreen, double zoom = 4, PhysicalRect? monitor = null) =>
        ModeGeometry.Compute(mode, monitor ?? s_monitor, anchor, zoom, LensSize.Default, DockLayout.Default).Source;

    private static PhysicalPoint Middle(PhysicalRect r) => new(r.X + (r.Width / 2), r.Y + (r.Height / 2));

    private static TrackingSnapshot Focus(PhysicalRect rect, double at) => new(rect, Ticks(at), null, 0);

    private static TrackingSnapshot Caret(PhysicalRect rect, double at) => new(null, 0, rect, Ticks(at));

    private static AnchorInput Input(
        double seconds, TrackingSnapshot? tracking, ZoomMode mode = ZoomMode.Fullscreen, double zoom = 4,
        PhysicalRect? monitor = null, bool mouseInside = true) =>
        new(Ticks(seconds), s_mouse, mouseInside, monitor ?? s_monitor, mode, zoom, LensSize.Default, DockLayout.Default,
            tracking ?? TrackingSnapshot.Empty, s_instant);

    private static long Ticks(double seconds) => (long)(seconds * Stopwatch.Frequency);
}
