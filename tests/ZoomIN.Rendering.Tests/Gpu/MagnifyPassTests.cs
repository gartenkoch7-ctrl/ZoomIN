using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>
/// Renders a known test image through Magnify.hlsl on WARP and checks sample pixels.
/// Source image (64×64): left half red, right half blue, top-left 16×16 quadrant green.
/// </summary>
public sealed class MagnifyPassTests
{
    private const int Size = 64;
    private static readonly PhysicalRect s_monitor = new(0, 0, Size, Size);

    [Fact]
    public void ZoomOne_ReproducesSourceImage()
    {
        var pixels = Render(new PhysicalRect(0, 0, Size, Size));

        Assert.Equal(TestImage.Green, pixels.At(4, 4));
        Assert.Equal(TestImage.Red, pixels.At(24, 40));
        Assert.Equal(TestImage.Blue, pixels.At(56, 40));
    }

    [Fact]
    public void ZoomTwo_LeftHalf_FillsTargetWithLeftHalfOfSource()
    {
        // Left 32×32 of the source: green quadrant (16×16) in the top-left, red elsewhere.
        var pixels = Render(new PhysicalRect(0, 0, Size / 2, Size / 2));

        Assert.Equal(TestImage.Green, pixels.At(8, 8));
        Assert.Equal(TestImage.Green, pixels.At(28, 28));
        Assert.Equal(TestImage.Red, pixels.At(40, 40));
        Assert.Equal(TestImage.Red, pixels.At(60, 4));
    }

    [Fact]
    public void ZoomTwo_RightHalf_ShowsOnlyBlue()
    {
        var pixels = Render(new PhysicalRect(Size / 2, Size / 2, Size / 2, Size / 2));

        Assert.Equal(TestImage.Blue, pixels.At(2, 2));
        Assert.Equal(TestImage.Blue, pixels.At(61, 61));
    }

    [Fact]
    public void Output_IsOpaque()
    {
        var pixels = Render(new PhysicalRect(0, 0, Size, Size));

        Assert.Equal(255, pixels.At(10, 50).A);
    }

    private static PixelBuffer Render(PhysicalRect source)
    {
        using var gpu = GpuDevice.CreateWarp();
        using var image = TestImage.Create(gpu.Device, Size);
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new MagnifyPass(gpu.Device);

        pass.Draw(gpu.Context, image.View, target.View, Size, Size, MagnifyConstants.FromSourceRect(source, s_monitor));

        return target.ReadBack(gpu.Device, gpu.Context);
    }
}
