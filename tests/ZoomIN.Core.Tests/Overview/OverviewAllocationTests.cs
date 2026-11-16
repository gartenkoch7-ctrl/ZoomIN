using ZoomIN.Core.Modes;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Overview;

/// <summary>"0 B per frame" (AGENTS.md 2.3) for what the render loop calls for the overview (B2).</summary>
public sealed class OverviewAllocationTests
{
    private const int Frames = 10_000;
    private const string Monitor = @"\\.\DISPLAY1";

    [Fact]
    public void FrameAndStart_DoNotAllocate()
    {
        var monitor = new PhysicalRect(0, 0, 1920, 1080);
        var model = new OverviewModel(TimeProvider.System);
        model.Press(Monitor, ZoomMode.Fullscreen, 4);
        var sum = Frame(model, monitor, 0); // warm-up / JIT

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Frames; i++)
        {
            sum += Frame(model, monitor, i);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sum > 0);
    }

    [Fact]
    public void ViewRequests_PlacementFor_DoesNotAllocate()
    {
        var requests = new ViewCenterRequests();
        requests.Request(Monitor, new PhysicalPoint(10, 20), 4, 1);
        var sum = requests.PlacementFor(Monitor).Zoom;

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Frames; i++)
        {
            sum += requests.PlacementFor(i % 2 == 0 ? Monitor : @"\\.\DISPLAY2").Zoom;
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sum > 0);
    }

    private static double Frame(OverviewModel model, PhysicalRect monitor, int i)
    {
        if (model.Current is not { } session || session.MonitorId != Monitor)
        {
            return 0;
        }

        var mouse = new PhysicalPoint(i % 1920, i % 1080);
        session.CaptureStart(monitor, mouse);
        return OverviewFrame.Rect(session, monitor, mouse, 1 + (i % 8), LensSize.Default, DockLayout.Default).Width;
    }
}
