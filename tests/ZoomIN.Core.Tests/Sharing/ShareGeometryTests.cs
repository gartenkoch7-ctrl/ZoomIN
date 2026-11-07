using ZoomIN.Core.Sharing;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Sharing;

public sealed class ShareGeometryTests
{
    private static readonly PhysicalRect s_left = new(0, 0, 1920, 1080);
    private static readonly PhysicalRect s_right = new(1920, 0, 2560, 1440);

    [Fact]
    public void NoArea_ClipsToNothing()
    {
        Assert.False(ShareGeometry.TryClip(null, s_left, out _));
    }

    [Fact]
    public void WindowInsideMonitor_StaysUnchanged()
    {
        var window = new PhysicalRect(100, 200, 800, 600);

        Assert.True(ShareGeometry.TryClip(window, s_left, out var clipped));
        Assert.Equal(window, clipped);
    }

    [Fact]
    public void WindowAcrossTwoMonitors_IsSplit()
    {
        var window = new PhysicalRect(1520, 100, 800, 600);

        Assert.True(ShareGeometry.TryClip(window, s_left, out var onLeft));
        Assert.True(ShareGeometry.TryClip(window, s_right, out var onRight));

        Assert.Equal(new PhysicalRect(1520, 100, 400, 600), onLeft);
        Assert.Equal(new PhysicalRect(1920, 100, 400, 600), onRight);
    }

    [Fact]
    public void WindowOnOtherMonitor_DoesNotTouch()
    {
        var window = new PhysicalRect(2000, 100, 800, 600);

        Assert.False(ShareGeometry.TryClip(window, s_left, out _));
    }

    [Fact]
    public void WindowEdgeTouchingMonitorEdge_DoesNotCount()
    {
        var window = new PhysicalRect(1920, 0, 100, 100);

        Assert.False(ShareGeometry.TryClip(window, s_left, out _));
    }

    [Fact]
    public void ScreenShare_CoversWholeMonitorOnly()
    {
        Assert.True(ShareGeometry.TryClip(s_right, s_right, out var clipped));
        Assert.Equal(s_right, clipped);
        Assert.False(ShareGeometry.TryClip(s_right, s_left, out _));
    }
}
