using ZoomIN.Core.Filters;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>Colour matrix and smoothing modes on WARP, with the red/blue/green test image.</summary>
public sealed class MagnifyFilterTests
{
    private const int Size = 64;
    private static readonly PhysicalRect s_monitor = new(0, 0, Size, Size);

    [Theory]
    [InlineData(Smoothing.Text)]
    [InlineData(Smoothing.Soft)]
    [InlineData(Smoothing.Pixel)]
    public void EverySmoothing_KeepsSolidAreasExact(Smoothing smoothing)
    {
        var pixels = Render(ColorMatrix.Identity, smoothing);

        Assert.Equal(TestImage.Red, pixels.At(24, 40));
        Assert.Equal(TestImage.Blue, pixels.At(56, 40));
    }

    [Fact]
    public void Inverted_TurnsRedIntoCyan_AndBlueIntoYellow()
    {
        var pixels = Render(ColorFilters.For(FilterId.Inverted), Smoothing.Pixel);

        Assert.Equal(new Bgra(255, 255, 0, 255), pixels.At(24, 40));
        Assert.Equal(new Bgra(0, 255, 255, 255), pixels.At(56, 40));
    }

    [Fact]
    public void Grayscale_MakesChannelsEqual()
    {
        var pixel = Render(ColorFilters.For(FilterId.Grayscale), Smoothing.Pixel).At(24, 40);

        Assert.Equal(pixel.R, pixel.G);
        Assert.Equal(pixel.G, pixel.B);
        Assert.InRange(pixel.R, 53, 55); // red luma 0.2126 · 255
    }

    [Fact]
    public void Pixel_KeepsHardEdge_SoftBlendsIt()
    {
        // Zoom 4× onto the red/blue edge at x = 32: the edge lands in the middle of the target.
        var source = new PhysicalRect(24, 24, 16, 16);

        var pixel = RenderSource(source, Smoothing.Pixel).At(33, 40);
        var soft = RenderSource(source, Smoothing.Soft).At(31, 40);

        Assert.True(pixel == TestImage.Red || pixel == TestImage.Blue, $"pixel mode must not blend, got {pixel}");
        Assert.True(soft.R is > 0 and < 255 && soft.B is > 0 and < 255, $"soft mode should blend at the edge, got {soft}");
    }

    private static PixelBuffer Render(ColorMatrix color, Smoothing smoothing) =>
        Draw(MagnifyConstants.Fullscreen(s_monitor, s_monitor, color, smoothing));

    private static PixelBuffer RenderSource(PhysicalRect source, Smoothing smoothing) =>
        Draw(MagnifyConstants.Fullscreen(source, s_monitor, ColorMatrix.Identity, smoothing));

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
