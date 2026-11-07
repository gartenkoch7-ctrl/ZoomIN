using ZoomIN.Core.Filters;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>
/// A colour region gets its own matrix (identity for a toast: original colours) while the
/// monitor's filter applies everywhere else. On WARP, with the red/blue/green test image.
/// </summary>
public sealed class MagnifyRegionTests
{
    private const int Size = 64;
    private static readonly PhysicalRect s_monitor = new(0, 0, Size, Size);
    private static readonly Bgra s_cyan = new(255, 255, 0, 255);
    private static readonly Bgra s_yellow = new(0, 255, 255, 255);
    private static readonly ColorMatrix s_inverted = ColorFilters.For(ColorFilter.Inverted);

    [Fact]
    public void IdentityRegion_KeepsOriginalColours_OutsideIsFiltered()
    {
        var blueHalf = new PhysicalRect(32, 0, 32, Size);

        var pixels = Draw(MagnifyConstants.Fullscreen(s_monitor, s_monitor, s_inverted, Smoothing.Pixel, Original(blueHalf)));

        Assert.Equal(TestImage.Blue, pixels.At(56, 40));
        Assert.Equal(s_cyan, pixels.At(24, 40));
    }

    [Fact]
    public void WithoutRegion_EverythingIsFiltered()
    {
        var pixels = Draw(MagnifyConstants.Fullscreen(s_monitor, s_monitor, s_inverted, Smoothing.Pixel));

        Assert.Equal(s_yellow, pixels.At(56, 40));
        Assert.Equal(s_cyan, pixels.At(24, 40));
    }

    [Fact]
    public void Region_IsInDesktopSpace_AndFollowsTheZoom()
    {
        // 2× onto the right half: the region x 48..64 fills the right half of the viewport.
        var source = new PhysicalRect(32, 32, 32, 32);
        var area = new PhysicalRect(48, 32, 16, 32);

        var pixels = Draw(MagnifyConstants.Fullscreen(source, s_monitor, s_inverted, Smoothing.Pixel, Original(area)));

        Assert.Equal(TestImage.Blue, pixels.At(48, 20));
        Assert.Equal(s_yellow, pixels.At(16, 20));
    }

    [Fact]
    public void Region_WorksInsideAFrame()
    {
        var target = new PhysicalRect(8, 8, 48, 48);
        var area = new PhysicalRect(0, 0, 32, Size);

        var pixels = Draw(MagnifyConstants.WithFrame(
            s_monitor, s_monitor, target, s_monitor, FrameColor.Default, s_inverted, Smoothing.Pixel, Original(area)));

        Assert.Equal(TestImage.Red, pixels.At(26, 40));
        Assert.Equal(s_yellow, pixels.At(50, 40));
    }

    [Fact]
    public void RegionOnAnotherMonitor_ChangesNothing()
    {
        var elsewhere = new PhysicalRect(200, 0, 64, 64);

        var pixels = Draw(MagnifyConstants.Fullscreen(s_monitor, s_monitor, s_inverted, Smoothing.Pixel, Original(elsewhere)));

        Assert.Equal(s_yellow, pixels.At(56, 40));
    }

    [Fact]
    public void RegionMatrix_ReplacesTheMonitorFilter()
    {
        // Filter A = identity, region B = inverted: only the region changes colour.
        var region = new ColorRegion(new PhysicalRect(32, 0, 32, Size), s_inverted);

        var pixels = Draw(MagnifyConstants.Fullscreen(s_monitor, s_monitor, ColorMatrix.Identity, Smoothing.Pixel, region));

        Assert.Equal(s_yellow, pixels.At(56, 40));
        Assert.Equal(TestImage.Red, pixels.At(24, 40));
    }

    private static ColorRegion Original(PhysicalRect area) => new(area, ColorMatrix.Identity);

    private static PixelBuffer Draw(MagnifyConstants constants)
    {
        using var gpu = GpuDevice.CreateWarp();
        using var image = TestImage.Create(gpu.Device, Size);
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new MagnifyPass(gpu.Device);

        pass.Draw(gpu.Context, image.View, target.View, Size, Size, constants);

        return target.ReadBack(gpu.Device, gpu.Context);
    }
}
