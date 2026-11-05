using ZoomIN.Core.Modes;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Modes;

public sealed class ViewCenteringTests
{
    public static TheoryData<ZoomMode, double> Cases()
    {
        var data = new TheoryData<ZoomMode, double>();
        foreach (var mode in new[] { ZoomMode.Fullscreen, ZoomMode.Lens, ZoomMode.Docked })
        {
            foreach (var zoom in new[] { 1.0, 1.5, 2.0, 4.0, 16.0 })
            {
                data.Add(mode, zoom);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Anchor_PutsPointInTheMiddle_OrStopsAtTheMonitorEdge(ZoomMode mode, double zoom)
    {
        // Left of the primary monitor with a negative origin: multi-monitor coordinates.
        var monitor = new PhysicalRect(-2560, -200, 2560, 1440);
        for (var x = monitor.X - 50; x <= monitor.Right + 50; x += 37)
        {
            for (var y = monitor.Y - 50; y <= monitor.Bottom + 50; y += 41)
            {
                var point = new PhysicalPoint(x, y);
                var anchor = ViewCentering.AnchorFor(mode, monitor, point, zoom, LensSize.Default, DockLayout.Default);
                var source = ModeGeometry.Compute(mode, monitor, anchor, zoom, LensSize.Default, DockLayout.Default).Source;

                var area = mode == ZoomMode.Docked ? ModeGeometry.RestRect(monitor, DockLayout.Default) : monitor;
                var expectedX = Math.Clamp(x, area.X + (source.Width / 2), area.Right - (source.Width / 2));
                var expectedY = Math.Clamp(y, area.Y + (source.Height / 2), area.Bottom - (source.Height / 2));
                Assert.Equal(expectedX, source.X + (source.Width / 2), 6);
                Assert.Equal(expectedY, source.Y + (source.Height / 2), 6);
            }
        }
    }

    [Fact]
    public void Fullscreen_FocusNearTheEdge_IsNotShownAtItsOwnPosition()
    {
        // The cause of the original bug: used as the fixpoint, (400, 300) would stay at (400, 300)
        // on screen, in the top-left corner of the view instead of its middle.
        var monitor = new PhysicalRect(0, 0, 1920, 1080);
        var focus = new PhysicalPoint(400, 300);

        var anchor = ViewCentering.AnchorFor(ZoomMode.Fullscreen, monitor, focus, 4, LensSize.Default, DockLayout.Default);
        var source = ModeGeometry.Compute(ZoomMode.Fullscreen, monitor, anchor, 4, LensSize.Default, DockLayout.Default).Source;

        Assert.Equal(new PhysicalRect(160, 165, 480, 270), source);
    }
}
