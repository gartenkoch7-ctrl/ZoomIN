using Vortice.Mathematics;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>
/// Lens/docked drawing on WARP: target 16..48 inside a 4 px frame (viewport 12..52) on a
/// cleared, transparent 64×64 target. Source: the blue right half of the test image.
/// </summary>
public sealed class MagnifyFrameTests
{
    private const int Size = 64;
    private static readonly PhysicalRect s_monitor = new(0, 0, Size, Size);
    private static readonly PhysicalRect s_target = new(16, 16, 32, 32);
    private static readonly PhysicalRect s_viewport = new(12, 12, 40, 40);

    [Fact]
    public void OutsideViewport_StaysTransparent()
    {
        var pixels = Render();

        Assert.Equal(new Bgra(0, 0, 0, 0), pixels.At(4, 4));
        Assert.Equal(new Bgra(0, 0, 0, 0), pixels.At(60, 60));
    }

    [Fact]
    public void FrameArea_HasFrameColor()
    {
        var pixels = Render();
        var frame = pixels.At(13, 30);

        Assert.InRange(frame.R, 254, 255);
        Assert.InRange(frame.G, 193, 195);
        Assert.InRange(frame.B, 7, 9);
        Assert.Equal(255, frame.A);
    }

    [Fact]
    public void Target_ShowsMagnifiedSource()
    {
        var pixels = Render();

        Assert.Equal(TestImage.Blue, pixels.At(20, 20));
        Assert.Equal(TestImage.Blue, pixels.At(44, 44));
    }

    private static PixelBuffer Render()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var image = TestImage.Create(gpu.Device, Size);
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new MagnifyPass(gpu.Device);
        var source = new PhysicalRect(Size / 2, 0, Size / 2, Size / 2);
        var constants = MagnifyConstants.WithFrame(source, s_monitor, s_target, s_viewport, FrameColor.Default);

        gpu.Context.ClearRenderTargetView(target.View, new Color4(0, 0, 0, 0));
        pass.Draw(gpu.Context, image.View, target.View, 12, 12, 40, 40, constants);

        return target.ReadBack(gpu.Device, gpu.Context);
    }
}
