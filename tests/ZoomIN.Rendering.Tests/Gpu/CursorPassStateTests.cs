using Vortice.Mathematics;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>
/// State set once per layer, skipped constant uploads and the last-texture cache of
/// <see cref="CursorPass"/> (M14 step 2), on WARP.
/// </summary>
public sealed class CursorPassStateTests
{
    private const int Size = 64;
    private static readonly FrameColor s_red = FrameColor.From(new RgbColor(255, 0, 0));
    private static readonly FrameColor s_green = FrameColor.From(new RgbColor(0, 255, 0));

    [Fact]
    public void SeveralDrawsInOneLayer_EachUseTheirOwnConstants()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new CursorPass(gpu.Device);
        gpu.Context.ClearRenderTargetView(target.View, new Color4(0, 0, 0, 0));

        // Red, green, red again: the third draw must upload again although it repeats the first.
        pass.DrawRect(gpu.Context, target.View, 0, 0, 16, 16, CursorConstants.ForRect(0.5f, 0.5f, s_red));
        pass.DrawRect(gpu.Context, target.View, 16, 0, 16, 16, CursorConstants.ForRect(0.5f, 0.5f, s_green));
        pass.DrawRect(gpu.Context, target.View, 32, 0, 16, 16, CursorConstants.ForRect(0.5f, 0.5f, s_red));
        pass.End(gpu.Context);
        var pixels = target.ReadBack(gpu.Device, gpu.Context);

        Assert.Equal(new Bgra(0, 0, 255, 255), pixels.At(8, 8));
        Assert.Equal(new Bgra(0, 255, 0, 255), pixels.At(24, 8));
        Assert.Equal(new Bgra(0, 0, 255, 255), pixels.At(40, 8));
    }

    [Fact]
    public void LayerAfterMagnify_BindsItsStateAgain()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var image = TestImage.Create(gpu.Device, Size);
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var cursor = new CursorPass(gpu.Device);
        using var magnify = new MagnifyPass(gpu.Device);
        var monitor = new PhysicalRect(0, 0, Size, Size);
        var constants = CursorConstants.ForRect(0.5f, 0.5f, s_green);

        // Frame 1 and 2 as in RenderSession: magnify, then a cursor layer with the same constants.
        for (var frame = 0; frame < 2; frame++)
        {
            magnify.Draw(gpu.Context, image.View, target.View, Size, Size, MagnifyConstants.FromSourceRect(monitor, monitor));
            cursor.DrawRect(gpu.Context, target.View, 40, 40, 8, 8, constants);
            cursor.End(gpu.Context);
        }

        var pixels = target.ReadBack(gpu.Device, gpu.Context);

        Assert.Equal(new Bgra(0, 255, 0, 255), pixels.At(44, 44));
        Assert.Equal(TestImage.Green, pixels.At(4, 4)); // magnify drew opaque, without blending
        Assert.Equal(TestImage.Blue, pixels.At(56, 20));
    }

    [Fact]
    public void Texture_SwitchingBackToAShape_ReturnsItsOwnTexture()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var pass = new CursorPass(gpu.Device);
        var source = new CountingSource();

        var first = pass.Texture(1, source);
        var second = pass.Texture(2, source);

        Assert.Same(first, pass.Texture(1, source));
        Assert.Same(second, pass.Texture(2, source));
        Assert.NotSame(first, second);
        Assert.Equal(2, source.Loads);
    }

    [Fact]
    public void StandardTexture_DependsOnHandleAndSharpSize()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var pass = new CursorPass(gpu.Device);
        var source = new CountingSource();

        var small = pass.StandardTexture(1, 1, source);
        var sharp = pass.StandardTexture(1, 2, source);

        Assert.NotSame(small, sharp);
        Assert.Same(small, pass.StandardTexture(1, 1, source));
        Assert.Same(sharp, pass.StandardTexture(1, 2, source));
        Assert.Equal(2, source.Loads);
    }

    private sealed class CountingSource : ICursorShapeSource
    {
        public int Loads { get; private set; }

        public bool TryGetCurrent(out nint handle)
        {
            handle = 1;
            return true;
        }

        public nint ArrowHandle() => 1;

        public CursorShape? Load(nint handle) => Shape();

        public CursorShape? LoadStandard(nint handle, double scale) => Shape();

        private CursorShape Shape()
        {
            Loads++;
            return new CursorShape(1, 4, 4, 0, 0, new byte[4 * 4 * 4]);
        }
    }
}
