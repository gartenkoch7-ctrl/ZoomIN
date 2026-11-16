using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Filters;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>
/// Idle frames (M14 step 3): without a new desktop image the smart inversion weights settle and the
/// ambient pass stops; a new image starts the smoothing again.
/// </summary>
public sealed class SmartInvertSettleTests
{
    private const int Size = 64;

    [Fact]
    public void WithoutNewImages_TheSmoothingSettles_AndANewImageRestartsIt()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var image = TestImage.Create(gpu.Device, Size);
        using var layer = SmartInvertLayer.Create(gpu.Device, Size, Size, @"\\.\DISPLAY1", new SmartInvertHealth(), NullLogger.Instance);

        Assert.NotNull(layer.Prepare(gpu.Context, image.View, needed: true, protectPhotos: true, inputChanged: true));
        Assert.True(layer.Settling);

        // Six time constants (0.3 s) after the last change the weights are final.
        Thread.Sleep(400);
        var settled = layer.Prepare(gpu.Context, image.View, needed: true, protectPhotos: true, inputChanged: false);

        Assert.NotNull(settled);
        Assert.False(layer.Settling);

        layer.Prepare(gpu.Context, image.View, needed: true, protectPhotos: true, inputChanged: true);

        Assert.True(layer.Settling);
    }

    [Fact]
    public void NotNeeded_IsNeverSettling()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var image = TestImage.Create(gpu.Device, Size);
        using var layer = SmartInvertLayer.Create(gpu.Device, Size, Size, @"\\.\DISPLAY1", new SmartInvertHealth(), NullLogger.Instance);
        layer.Prepare(gpu.Context, image.View, needed: true, protectPhotos: true);

        Assert.Null(layer.Prepare(gpu.Context, image.View, needed: false, protectPhotos: true));
        Assert.False(layer.Settling);
    }
}
