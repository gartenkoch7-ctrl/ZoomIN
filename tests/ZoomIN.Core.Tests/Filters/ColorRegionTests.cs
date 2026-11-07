using ZoomIN.Core.Filters;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Filters;

public sealed class ColorRegionTests
{
    private static readonly PhysicalRect s_toast = new(1540, 820, 360, 200);
    private static readonly TrackingSnapshot s_withToast = new(null, 0, null, 0, s_toast, 1);

    [Fact]
    public void VisibleToast_GetsIdentityMatrix() =>
        Assert.Equal(new ColorRegion(s_toast, ColorMatrix.Identity), ColorRegion.For(s_withToast, TrackingOptions.Default));

    [Fact]
    public void NoToast_NoRegion() =>
        Assert.Null(ColorRegion.For(TrackingSnapshot.Empty, TrackingOptions.Default));

    [Fact]
    public void OriginalColoursOff_NoRegion() =>
        Assert.Null(ColorRegion.For(s_withToast, TrackingOptions.Default with { NotificationColors = false }));
}
