using System.Diagnostics;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Overview;

/// <summary>Zooming in at the overview frame through A1's anchor placement (margin mode).</summary>
public sealed class OverviewPlacementTests
{
    private const string Monitor = @"\\.\DISPLAY1";
    private static readonly PhysicalRect s_monitor = new(0, 0, 1920, 1080);

    [Theory]
    [InlineData(1700, 900)]
    [InlineData(1900, 1070)] // the frame stops at the corner, so does the view
    public void ZoomIn_PlacesTheViewOnTheFrame(double x, double y)
    {
        const double level = 6;
        var session = new OverviewSession(1, Monitor, ZoomMode.Fullscreen, level, 0);
        session.CaptureStart(new PhysicalRect(200, 100, 320, 180), new PhysicalPoint(300, 150));
        var mouse = new PhysicalPoint(x, y);
        var frame = OverviewFrame.Rect(session, s_monitor, mouse, level, LensSize.Default, DockLayout.Default);
        var requests = new ViewCenterRequests();
        var options = TrackingOptions.Default with { PanSeconds = 0 };
        var anchors = new AnchorController();
        var now = Stopwatch.GetTimestamp();

        // The renderer ran before the request; then the overview asks for the frame centre.
        anchors.Next(Input(now, mouse, level, options, requests.PlacementFor(Monitor)));
        requests.Request(Monitor, OverviewFrame.Center(frame), level, now + 1);
        var anchor = anchors.Next(Input(now + 2, mouse, level, options, requests.PlacementFor(Monitor)));
        var view = ModeGeometry.Compute(ZoomMode.Fullscreen, s_monitor, anchor, level, LensSize.Default, DockLayout.Default).Source;

        Assert.Equal(frame.X, view.X, 3);
        Assert.Equal(frame.Y, view.Y, 3);
        Assert.Equal(frame.Width, view.Width, 3);
    }

    private static AnchorInput Input(long now, PhysicalPoint mouse, double zoom, TrackingOptions options, ViewPlacement placement) =>
        new(now, mouse, true, s_monitor, ZoomMode.Fullscreen, zoom, LensSize.Default, DockLayout.Default,
            new TrackingSnapshot(null, 0, null, 0), options, placement);
}
