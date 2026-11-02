using ZoomIN.Core.Pointer;
using ZoomIN.Core.Tracking;

namespace ZoomIN.Core.Tests.Pointer;

public sealed class PointerPreviewTests
{
    // Premultiplied BGRA: white, opaque.
    private static readonly (double B, double G, double R, double A) s_white = (255, 255, 255, 255);
    private static readonly (double B, double G, double R, double A) s_black = (0, 0, 0, 255);

    [Fact]
    public void Tint_ColoursWhite_KeepsBlackOutline()
    {
        var style = PointerStyle.Default with { ColorMode = PointerColorMode.Tint, Color = new RgbColor(255, 0, 0) };

        var white = PointerPreview.Colored(s_white, style);
        var black = PointerPreview.Colored(s_black, style);

        Assert.Equal((0, 0, 255, 255), (Math.Round(white.B), Math.Round(white.G), Math.Round(white.R), white.A));
        Assert.Equal(s_black, black);
    }

    [Fact]
    public void Inverted_SwapsBlackAndWhite()
    {
        var style = PointerStyle.Default with { ColorMode = PointerColorMode.Inverted };

        Assert.Equal(s_black, PointerPreview.Colored(s_white, style));
        Assert.Equal(s_white, PointerPreview.Colored(s_black, style));
    }

    [Fact]
    public void Render_ScalesAndAddsRim()
    {
        var pixels = Enumerable.Repeat((byte)255, 4 * 4 * 4).ToArray();
        var shape = new CursorShape(1, 4, 4, 0, 0, pixels);
        var style = PointerStyle.Default with { Rim = true, RimWidth = 2 };

        var (image, width, height) = PointerPreview.Render(shape, style, 2);

        // 4 px × 2 plus a 2 px rim on each side.
        Assert.Equal(12, width);
        Assert.Equal(12, height);
        Assert.Equal(width * height * 4, image.Length);
        Assert.Equal(255, image[((1 * width) + 6) * 4 + 3]); // rim pixel above the pointer is opaque
    }

    [Fact]
    public void Render_DenseImage_CoversOnlyItsBaseSize()
    {
        // A standard pointer loaded at 16 px that stands for a 4 px pointer at size 1×.
        var pixels = Enumerable.Repeat((byte)255, 16 * 16 * 4).ToArray();
        var shape = new CursorShape(1, 16, 16, 0, 0, pixels, BaseSize: 4);
        var style = PointerStyle.Default with { Rim = false };

        var (_, width, height) = PointerPreview.Render(shape, style, 2);

        Assert.Equal(4.0, shape.Density);
        Assert.Equal(8, width);
        Assert.Equal(8, height);
    }
}
