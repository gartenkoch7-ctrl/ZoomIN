using ZoomIN.Core.Sharing;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Sharing;

public sealed class ShareDetectorTests
{
    private static readonly PhysicalRect s_left = new(0, 0, 1920, 1080);
    private static readonly PhysicalRect s_right = new(1920, 0, 2560, 1440);
    private static readonly PhysicalRect[] s_monitors = [s_left, s_right];

    private static MeetingWindow Frame(PhysicalRect bounds) => new(1, bounds, Topmost: true, ClickThrough: true);

    [Fact]
    public void NoFrame_NoShare()
    {
        MeetingWindow[] windows = [new(1, new PhysicalRect(100, 100, 1200, 800), Topmost: false, ClickThrough: false)];

        Assert.Null(ShareDetector.Detect(windows, s_monitors));
    }

    [Fact]
    public void FrameAroundMonitor_IsScreenShare_WithMonitorBounds()
    {
        MeetingWindow[] windows = [Frame(new PhysicalRect(1922, 2, 2556, 1436))];

        Assert.Equal(new SharedArea(ShareKind.Screen, s_right), ShareDetector.Detect(windows, s_monitors));
    }

    [Fact]
    public void FrameAroundWindow_IsWindowShare_WithFrameBounds()
    {
        var window = new PhysicalRect(200, 150, 1000, 700);

        Assert.Equal(new SharedArea(ShareKind.Window, window), ShareDetector.Detect([Frame(window)], s_monitors));
    }

    [Fact]
    public void FrameNotTopmostOrNotClickThrough_IsIgnored()
    {
        var bounds = new PhysicalRect(200, 150, 1000, 700);
        MeetingWindow[] windows =
        [
            new(1, bounds, Topmost: false, ClickThrough: true),
            new(2, bounds, Topmost: true, ClickThrough: false),
        ];

        Assert.Null(ShareDetector.Detect(windows, s_monitors));
    }

    [Fact]
    public void SmallClickThroughWindow_IsNotAFrame()
    {
        Assert.Null(ShareDetector.Detect([Frame(new PhysicalRect(10, 10, 300, 60))], s_monitors));
    }

    [Fact]
    public void SeveralFrames_LargestWins()
    {
        var small = new PhysicalRect(10, 10, 400, 300);
        var large = new PhysicalRect(500, 100, 1000, 800);

        Assert.Equal(large, ShareDetector.Detect([Frame(small), Frame(large)], s_monitors)?.Bounds);
    }
}
