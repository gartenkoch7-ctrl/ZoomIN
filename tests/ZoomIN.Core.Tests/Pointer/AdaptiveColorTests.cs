using ZoomIN.Core.Pointer;
using ZoomIN.Core.Tracking;

namespace ZoomIN.Core.Tests.Pointer;

/// <summary>Colour rule of the adaptive pointer (M15): body and outline from the background.</summary>
public sealed class AdaptiveColorTests
{
    private static readonly PointerStyle s_style = PointerStyle.Default with { ColorMode = PointerColorMode.Adaptive };
    private static readonly (double B, double G, double R, double A) s_white = (255, 255, 255, 255);
    private static readonly (double B, double G, double R, double A) s_black = (0, 0, 0, 255);

    [Fact]
    public void OnBlack_BodyWhite_OutlineBlack()
    {
        Assert.Equal((255d, 255d, 255d), AdaptiveColor.Pick(1, 0, s_style));
        Assert.Equal((0d, 0d, 0d), AdaptiveColor.Pick(0, 0, s_style));
    }

    [Fact]
    public void OnWhite_BodyBlack_OutlineWhite()
    {
        Assert.Equal((0d, 0d, 0d), AdaptiveColor.Pick(1, 1, s_style));
        Assert.Equal((255d, 255d, 255d), AdaptiveColor.Pick(0, 1, s_style));
    }

    [Theory]
    [InlineData(0.49, 255)]
    [InlineData(0.50, 0)]
    [InlineData(0.51, 0)]
    public void Grey_SwitchesHardAtTheThreshold_NeverAMix(double background, double expected)
    {
        var (r, g, b) = AdaptiveColor.Pick(1, background, s_style);

        Assert.Equal((expected, expected, expected), (r, g, b));
    }

    [Fact]
    public void Threshold_MovesTheSwitch()
    {
        var style = s_style with { AdaptiveThreshold = 0.3 };

        Assert.True(AdaptiveColor.OnLight(0.35, style));
        Assert.False(AdaptiveColor.OnLight(0.25, style));
    }

    [Fact]
    public void OwnColourPair_IsUsed()
    {
        var style = s_style with { AdaptiveOnDark = RgbColor.Yellow, AdaptiveOnLight = new RgbColor(0, 0, 128) };

        Assert.Equal((255d, 214d, 0d), AdaptiveColor.Pick(1, 0.1, style));
        Assert.Equal((0d, 0d, 128d), AdaptiveColor.Pick(0, 0.1, style)); // outline on dark ground
        Assert.Equal((0d, 0d, 128d), AdaptiveColor.Pick(1, 0.9, style));
    }

    [Fact]
    public void Preview_ColouredPointer_UsesItsBrightness()
    {
        // Saturated blue (dark): counts as outline; on white ground the outline is white.
        var blue = PointerPreview.Colored((255, 0, 0, 255), s_style, backgroundLuma: 1);

        Assert.True(blue.R > 200 && blue.G > 200 && blue.B > 200);
        Assert.Equal(s_black, Round(PointerPreview.Colored(s_white, s_style, backgroundLuma: 1)));
        Assert.Equal(s_white, Round(PointerPreview.Colored(s_white, s_style, backgroundLuma: 0)));
    }

    private static (double B, double G, double R, double A) Round((double B, double G, double R, double A) p) =>
        (Math.Round(p.B), Math.Round(p.G), Math.Round(p.R), p.A);

    [Fact]
    public void Preview_HalfTransparentPixel_StaysPremultiplied()
    {
        var half = PointerPreview.Colored((128, 128, 128, 128), s_style, backgroundLuma: 0);

        Assert.Equal((128d, 128d, 128d, 128d), half);
    }

    [Fact]
    public void Preview_EdgeInTheMiddle_ColoursEachHalfForItsGround()
    {
        var shape = new CursorShape(1, 4, 4, 0, 0, Enumerable.Repeat((byte)255, 4 * 4 * 4).ToArray());
        var style = s_style with { Rim = false };

        var (image, width, _) = PointerPreview.Render(shape, style, 1, leftLuma: 0, rightLuma: 1);

        Assert.Equal(255, image[0]); // left half over dark: white body
        Assert.Equal(0, image[(width - 1) * 4]); // right half over light: black body
    }

    [Fact]
    public void Preview_InvertPixels_UseTheAdaptiveImage()
    {
        var black = new byte[] { 0, 0, 0, 255 };
        var white = new byte[] { 255, 255, 255, 255 };
        var shape = new CursorShape(1, 1, 1, 0, 0, black, AdaptivePixels: white);
        var style = s_style with { Rim = false };

        var (image, _, _) = PointerPreview.Render(shape, style, 1, leftLuma: 0, rightLuma: 0);

        Assert.Equal(255, image[0]); // body on dark ground, not outline
        Assert.Same(black, shape.PixelsFor(PointerColorMode.Original));
    }

    [Fact]
    public void Style_NeedsDesktop_OnlyInAdaptiveMode_AndDrawsAt1x()
    {
        Assert.True(s_style.NeedsDesktop);
        Assert.True(s_style.DrawsPointer(1.0));
        Assert.False(PointerStyle.Default.NeedsDesktop);
    }

    [Theory]
    [InlineData(0.0, 0.1)]
    [InlineData(0.5, 0.5)]
    [InlineData(2.0, 0.9)]
    public void Normalized_ClampsTheThreshold(double input, double expected)
    {
        Assert.Equal(expected, (s_style with { AdaptiveThreshold = input }).Normalized().AdaptiveThreshold);
    }

    [Fact]
    public void Normalized_UnknownColourMode_FallsBackToOriginal()
    {
        Assert.Equal(PointerColorMode.Original, (s_style with { ColorMode = (PointerColorMode)42 }).Normalized().ColorMode);
    }
}
