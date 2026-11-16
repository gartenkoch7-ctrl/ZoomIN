using BenchmarkDotNet.Attributes;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Benchmarks;

/// <summary>
/// What the render loop does per frame for the overview (M17 B2): read the running overview, size
/// and place its frame, look for a view request. Budget: 0 B allocated.
/// </summary>
public class OverviewBenchmarks
{
    private const string Monitor = @"\\.\DISPLAY1";
    private readonly PhysicalRect _monitor = new(0, 0, 3840, 2160);
    private readonly OverviewModel _overview = new(TimeProvider.System);
    private readonly ViewCenterRequests _requests = new();
    private int _frame;

    [GlobalSetup]
    public void Setup()
    {
        _overview.Press(Monitor, ZoomMode.Fullscreen, 6);
        _overview.Current!.CaptureStart(new PhysicalRect(1000, 600, 640, 360), new PhysicalPoint(1300, 800));
        _requests.Request(Monitor, new PhysicalPoint(1920, 1080), 6, 1);
    }

    [Benchmark]
    public double OverviewFrameRect()
    {
        _frame++;
        if (_overview.Current is not { } session || session.MonitorId != Monitor)
        {
            return 0;
        }

        var mouse = new PhysicalPoint(_frame % 3840, _frame % 2160);
        return OverviewFrame.Rect(session, _monitor, mouse, 6, LensSize.Default, DockLayout.Default).X;
    }

    [Benchmark]
    public long ViewRequestCheck() => _requests.PlacementFor(Monitor).At;
}
