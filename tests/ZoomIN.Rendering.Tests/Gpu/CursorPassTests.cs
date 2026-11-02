using Vortice.Mathematics;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Tracking;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>Pointer colour modes and the filled rectangle (crosshair line) of Cursor.hlsl on WARP.</summary>
public sealed class CursorPassTests
{
    private const int Size = 32;

    [Fact]
    public void Tint_ColoursAWhitePointer()
    {
        var tint = FrameColor.From(new RgbColor(255, 0, 0)) with { A = (float)PointerColorMode.Tint };

        var pixel = DrawWhitePointer(tint).At(8, 8);

        Assert.Equal(new Bgra(0, 0, 255, 255), pixel);
    }

    [Fact]
    public void Inverted_TurnsAWhitePointerBlack()
    {
        var tint = default(FrameColor) with { A = (float)PointerColorMode.Inverted };

        Assert.Equal(new Bgra(0, 0, 0, 255), DrawWhitePointer(tint).At(8, 8));
    }

    [Fact]
    public void Original_KeepsThePointer()
    {
        Assert.Equal(new Bgra(255, 255, 255, 255), DrawWhitePointer(default).At(8, 8));
    }

    [Fact]
    public void FilledRect_DrawsCrosshairLineWithOpacity()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new CursorPass(gpu.Device);
        gpu.Context.ClearRenderTargetView(target.View, new Color4(0, 0, 0, 0));

        var color = FrameColor.From(new RgbColor(0, 255, 255), 0.5);
        pass.DrawRect(gpu.Context, target.View, 0, 15, Size, 2, CursorConstants.ForRect(0.5f, 0.5f, color));
        var pixels = target.ReadBack(gpu.Device, gpu.Context);

        Assert.InRange(pixels.At(20, 15).G, 126, 129);
        Assert.InRange(pixels.At(20, 15).A, 126, 129);
        Assert.Equal(new Bgra(0, 0, 0, 0), pixels.At(20, 4));
    }

    // A 16×16 opaque white pointer at (0,0), no rim, scaled 1:1.
    private static PixelBuffer DrawWhitePointer(FrameColor tint)
    {
        using var gpu = GpuDevice.CreateWarp();
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new CursorPass(gpu.Device);
        var shape = new CursorShape(1, 16, 16, 0, 0, Enumerable.Repeat((byte)255, 16 * 16 * 4).ToArray());
        using var pointer = PointerTexture.Create(gpu.Device, shape);
        gpu.Context.ClearRenderTargetView(target.View, new Color4(0, 0, 0, 0));

        pass.DrawPointer(gpu.Context, target.View, pointer, 0, 0, 16, 16,
            CursorConstants.ForPointer(0, 0, 16, 16, 0, default, tint));
        return target.ReadBack(gpu.Device, gpu.Context);
    }
}
