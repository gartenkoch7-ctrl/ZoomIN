using ZoomIN.Core.Modes;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Overview;

/// <summary>Size and place of the overview frame (B2), and that zooming in at its centre shows it.</summary>
public sealed class OverviewFrameTests
{
    private static readonly PhysicalRect s_monitor = new(1920, 0, 1920, 1080);

    [Fact]
    public void Fullscreen_FrameIsTheViewAtTheUsersLevel_AroundTheMouse()
    {
        var session = Session(ZoomMode.Fullscreen);

        var rect = OverviewFrame.Rect(session, s_monitor, new PhysicalPoint(2880, 540), 4, LensSize.Default, DockLayout.Default);

        Assert.Equal(new PhysicalRect(2640, 405, 480, 270), rect);
    }

    [Fact]
    public void Lens_FrameIsWhatTheLensMagnifies()
    {
        var session = Session(ZoomMode.Lens);
        var lens = LensSize.Default;

        var rect = OverviewFrame.Rect(session, s_monitor, new PhysicalPoint(2880, 540), 2, lens, DockLayout.Default);

        Assert.Equal(1920 * lens.WidthFraction / 2, rect.Width, 6);
        Assert.Equal(1080 * lens.HeightFraction / 2, rect.Height, 6);
        Assert.Equal(new PhysicalPoint(2880, 540), OverviewFrame.Center(rect));
    }

    [Theory]
    [InlineData(1900, -50, 1920, 0)] // mouse on the monitor to the left, above the top
    [InlineData(4000, 2000, 3360, 810)] // beyond the bottom-right corner
    public void Frame_StaysInsideTheMonitor(double x, double y, double left, double top)
    {
        var rect = OverviewFrame.Rect(Session(ZoomMode.Fullscreen), s_monitor, new PhysicalPoint(x, y), 4, LensSize.Default, DockLayout.Default);

        Assert.Equal(new PhysicalRect(left, top, 480, 270), rect);
    }

    [Fact]
    public void Frame_StartsAsThePreviousView_AndMovesWithTheMouse()
    {
        var session = Session(ZoomMode.Fullscreen);
        session.CaptureStart(new PhysicalRect(2000, 100, 480, 270), new PhysicalPoint(2300, 200));

        var atStart = OverviewFrame.Rect(session, s_monitor, new PhysicalPoint(2300, 200), 4, LensSize.Default, DockLayout.Default);
        var moved = OverviewFrame.Rect(session, s_monitor, new PhysicalPoint(2400, 250), 4, LensSize.Default, DockLayout.Default);

        Assert.Equal(new PhysicalRect(2000, 100, 480, 270), atStart);
        Assert.Equal(new PhysicalRect(2100, 150, 480, 270), moved);
    }

    [Fact]
    public void Frame_AtLevelOne_CoversTheMonitor()
    {
        var rect = OverviewFrame.Rect(Session(ZoomMode.Fullscreen), s_monitor, new PhysicalPoint(2000, 10), 1, LensSize.Default, DockLayout.Default);

        Assert.Equal(s_monitor, rect);
    }

    [Theory]
    [InlineData(ZoomMode.Fullscreen, 2880, 540)]
    [InlineData(ZoomMode.Fullscreen, 1925, 1070)] // corner: the view stops at the edge like the frame
    [InlineData(ZoomMode.Lens, 2500, 300)]
    public void ZoomingIn_AtTheFrameCentre_ShowsTheFrame(ZoomMode mode, double x, double y)
    {
        const double level = 6;
        var rect = OverviewFrame.Rect(Session(mode), s_monitor, new PhysicalPoint(x, y), level, LensSize.Default, DockLayout.Default);

        // What the anchor does with a view request (A1 wiring): the fixpoint that centres the point.
        var anchor = ViewCentering.AnchorFor(mode, s_monitor, OverviewFrame.Center(rect), level, LensSize.Default, DockLayout.Default);
        var view = ModeGeometry.Compute(mode, s_monitor, anchor, level, LensSize.Default, DockLayout.Default).Source;

        Assert.Equal(rect.X, view.X, 6);
        Assert.Equal(rect.Y, view.Y, 6);
        Assert.Equal(rect.Width, view.Width, 6);
        Assert.Equal(rect.Height, view.Height, 6);
    }

    [Fact]
    public void CaptureStart_CountsOnlyOnce()
    {
        var session = Session(ZoomMode.Fullscreen);
        Assert.False(session.TryGetStart(out _, out _));

        session.CaptureStart(new PhysicalRect(1, 2, 3, 4), new PhysicalPoint(5, 6));
        session.CaptureStart(new PhysicalRect(9, 9, 9, 9), new PhysicalPoint(9, 9));

        Assert.True(session.TryGetStart(out var view, out var mouse));
        Assert.Equal(new PhysicalRect(1, 2, 3, 4), view);
        Assert.Equal(new PhysicalPoint(5, 6), mouse);
        Assert.Throws<ArgumentNullException>(() => OverviewFrame.Rect(null!, s_monitor, default, 2, LensSize.Default, DockLayout.Default));
        Assert.Throws<ArgumentNullException>(() => new OverviewSession(1, null!, ZoomMode.Lens, 2, 0));
    }

    private static OverviewSession Session(ZoomMode mode) => new(1, @"\\.\DISPLAY2", mode, 4, 0);
}
