using ZoomIN.Core.Filters;
using ZoomIN.Rendering.Gpu;
using static ZoomIN.Rendering.Tests.Gpu.EnhanceRender;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>
/// "Schärfe"/"Fett" (M18, D1) on WARP: edges get steeper, strokes wider, no halos under any
/// filter, ClearType fringes and photos are not made worse, and off draws exactly as before.
/// </summary>
public sealed class TextEnhanceTests
{
    private static readonly Bgra s_white = new(255, 255, 255, 255);
    private static readonly Bgra s_black = new(0, 0, 0, 255);
    private static readonly Bgra s_paper = new(200, 200, 200, 255);
    private static readonly Bgra s_ink = new(60, 60, 60, 255);
    private static readonly TextEnhancement s_hairBold = new(0, 0.001);

    // Black left of x = 32, white from there: one hard edge in the middle of the view.
    private static readonly Func<int, int, Bgra> s_edge = (x, _) => x < 32 ? s_black : s_white;

    // A one-pixel dark line at x = 32 on grey paper (grey so an overshoot could show either way).
    private static readonly Func<int, int, Bgra> s_line = (x, _) => x == 32 ? s_ink : s_paper;

    [Fact]
    public void Sharpness_MakesTheEdgeSteeper()
    {
        var off = Transition(Render(s_edge, TextEnhancement.Off));
        var sharp = Transition(Render(s_edge, new TextEnhancement(1, 0)));

        Assert.True(sharp < off, $"edge {sharp} px wide with sharpness, {off} px without");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void Bold_MakesTheStrokeWider(double bold)
    {
        var off = Dark(Render(s_line, TextEnhancement.Off));
        var wide = Dark(Render(s_line, new TextEnhancement(0, bold)));

        // Each side grows by about "bold" output pixels.
        Assert.True(wide >= off + (2 * bold) - 1, $"stroke {wide} px wide with bold {bold}, {off} px without");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(FilterId.Inverted)]
    [InlineData(FilterId.InvertedSmart)]
    public void FullStrength_NoHalo_UnderEveryFilter(FilterId? filter)
    {
        var exact = Range(Render(s_line, TextEnhancement.Off, filter, Smoothing.Pixel));
        var on = Range(Render(s_line, new TextEnhancement(1, 3), filter));

        // A halo is a ring lighter than the paper or darker than the ink (both after the filter).
        Assert.True(on.Low >= exact.Low - 1 && on.High <= exact.High + 1, $"range {on} with enhancement, ink to paper {exact}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(FilterId.Inverted)]
    [InlineData(FilterId.InvertedSmart)]
    public void Sharpness_AddsNoOvershootBeyondCatmullRom(FilterId? filter)
    {
        // Catmull-Rom rings slightly today; sharpness may reach the exact colours, but go no further.
        var off = Range(Render(s_line, TextEnhancement.Off, filter));
        var exact = Range(Render(s_line, TextEnhancement.Off, filter, Smoothing.Pixel));
        var on = Range(Render(s_line, new TextEnhancement(1, 0), filter));

        Assert.True(on.Low >= Math.Min(off.Low, exact.Low) - 1 && on.High <= Math.Max(off.High, exact.High) + 1,
            $"range {on} with sharpness, {off} without, ink to paper {exact}");
    }

    [Fact]
    public void Bold_WhiteStrokeOnBlack_GetsWider_WithoutDarkSpecks()
    {
        // A one-texel white stroke gets wider. Two white texels: Catmull-Rom overshoots past white
        // between them (1.125), which the light side of bold once turned black.
        static Bgra Line(int x, int y) => x == 32 ? s_white : s_black;
        static Bgra Bar(int x, int y) => x is 32 or 33 ? s_white : s_black;

        var line = Row(Render(Line, TextEnhancement.Off), 128).Count(l => l > 128);
        var wider = Row(Render(Line, new TextEnhancement(0, 2)), 128).Count(l => l > 128);
        var off = Row(Render(Bar, TextEnhancement.Off), 128);
        var bold = Row(Render(Bar, new TextEnhancement(0, 2)), 128);

        Assert.True(wider >= line + 3, $"white stroke {wider} px wide with bold, {line} px without");

        // Between the two white texel centres, where the overshoot is. (At the centres themselves the
        // 4 × 4 texel surroundings are half white, half black: which side bold takes there is open.)
        for (var x = TargetOf(32) + 1; x < TargetOf(33); x++)
        {
            Assert.True(bold[x] >= off[x] - 1, $"pixel {x} turned darker with bold: {bold[x]} instead of {off[x]}");
        }
    }

    [Theory]
    [InlineData(FilterId.Inverted)]
    [InlineData(FilterId.InvertedSmart)]
    public void Bold_WidensTheInvertedStrokeToo(FilterId filter)
    {
        var off = Light(Render(s_line, TextEnhancement.Off, filter));
        var wide = Light(Render(s_line, new TextEnhancement(0, 2), filter));

        Assert.True(wide >= off + 3, $"inverted stroke {wide} px wide with bold, {off} px without");
    }

    [Fact]
    public void Bold_DoesNotMakeClearTypeFringesMoreColourful()
    {
        // A black stroke with an orange fringe left and a blue one right, as ClearType draws them.
        static Bgra Text(int x, int y) => x switch
        {
            31 => new Bgra(40, 140, 230, 255),
            32 => s_black,
            33 => new Bgra(230, 120, 40, 255),
            _ => s_white,
        };

        var off = Colourful(Render(Text, TextEnhancement.Off));
        var bold = Colourful(Render(Text, new TextEnhancement(0, 3)));

        Assert.True(bold <= off, $"{bold} colourful pixels with bold, {off} without");
    }

    [Fact]
    public void ColourfulPhotos_StayAsTheyAre()
    {
        // Hard, saturated texture: every pixel has another hue, as in a detailed photo.
        static Bgra Photo(int x, int y)
        {
            var hue = ((x * 73) + (y * 151)) % 97 / 97.0 * 2 * Math.PI;
            static byte Channel(double angle) => (byte)(128 + (120 * Math.Sin(angle)));
            return new Bgra(Channel(hue), Channel(hue + 2.1), Channel(hue + 4.2), 255);
        }

        // The reference takes the same shader path with a bold too small to show.
        var photo = Render(Photo, s_hairBold);
        var bold = MeanDifference(photo, Render(Photo, new TextEnhancement(0, 3)));
        var sharp = MeanDifference(Render(Photo, TextEnhancement.Off), Render(Photo, new TextEnhancement(1, 0)));
        var text = MeanDifference(Render(s_line, s_hairBold), Render(s_line, new TextEnhancement(0, 3)));

        Assert.True(bold * 4 < text, $"photo changed by {bold:F2} with bold, text by {text:F2}");
        Assert.True(sharp * 4 < text, $"photo changed by {sharp:F2} with sharpness, text by {text:F2} with bold");
    }

    [Theory]
    [InlineData(1.0, Smoothing.Text)]
    [InlineData(8.0, Smoothing.Soft)]
    [InlineData(8.0, Smoothing.Pixel)]
    public void NotApplied_DrawsExactlyAsBefore(double zoom, Smoothing smoothing)
    {
        var enhance = new TextEnhancement(1, 3).At(zoom, smoothing);
        var plain = MagnifyConstants.Fullscreen(Source, Screen, ColorMatrix.Identity, smoothing);
        var resolved = MagnifyConstants.Fullscreen(Source, Screen, ColorMatrix.Identity, smoothing, enhance: enhance);

        Assert.False(resolved.Enhanced);
        Assert.Equal(Pixels(plain), Pixels(resolved));
    }

    // Target x of the centre of source texel x.
    private static int TargetOf(int x) => (int)((x + 0.5 - Source.X) * Zoom);

    // Output pixels of row 128 between 10 % and 90 % of the way from black to white.
    private static int Transition(PixelBuffer pixels) => Row(pixels, 128).Count(l => l is > 25 and < 230);

    private static int Dark(PixelBuffer pixels) => Row(pixels, 128).Count(l => l < 130);

    private static int Light(PixelBuffer pixels) => Row(pixels, 128).Count(l => l > 125);

    private static int Colourful(PixelBuffer pixels)
    {
        var count = 0;
        for (var y = 0; y < Target; y += 8)
        {
            for (var x = 0; x < Target; x++)
            {
                count += Chroma(pixels.At(x, y)) > 60 ? 1 : 0;
            }
        }

        return count;
    }

    private static double MeanDifference(PixelBuffer a, PixelBuffer b)
    {
        long sum = 0;
        for (var y = 0; y < Target; y++)
        {
            for (var x = 0; x < Target; x++)
            {
                sum += Math.Abs(Luma(a.At(x, y)) - Luma(b.At(x, y)));
            }
        }

        return sum / (double)(Target * Target);
    }

    private static Bgra[] Pixels(MagnifyConstants constants)
    {
        using var gpu = GpuDevice.CreateWarp();
        using var texture = TestImage.Create(gpu.Device, Image, s_line);
        using var target = RenderTarget.Create(gpu.Device, Target);
        using var pass = new MagnifyPass(gpu.Device);
        pass.Draw(gpu.Context, texture.View, target.View, Target, Target, constants);
        var pixels = target.ReadBack(gpu.Device, gpu.Context);
        return [.. Enumerable.Range(0, Target * Target).Select(i => pixels.At(i % Target, i / Target))];
    }
}
