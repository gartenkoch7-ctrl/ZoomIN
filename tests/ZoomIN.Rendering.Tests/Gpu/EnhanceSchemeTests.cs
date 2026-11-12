using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;
using ZoomIN.Rendering.Gpu;
using static ZoomIN.Rendering.Tests.Gpu.EnhanceRender;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>
/// M18 integration on WARP: "Schärfe"/"Fett" (D1) and own schemes (C) share the magnify pass.
/// PSEnhanced must still run the colour replacement and the scheme's palette entry after it.
/// </summary>
public sealed class EnhanceSchemeTests
{
    private static readonly Bgra s_paper = new(200, 200, 200, 255);
    private static readonly Bgra s_ink = new(60, 60, 60, 255);
    private static readonly Bgra s_white = new(255, 255, 255, 255);
    private static readonly Bgra s_yellow = new(0, 255, 255, 255);

    // Light becomes navy, dark becomes bright green: the ink turns into the bright colour.
    private static readonly CustomScheme s_twoColors =
        new() { Id = FilterId.Custom3, Kind = SchemeKind.TwoColors, Color1 = "#0D1B4C", Color2 = "#00E676" };

    private static readonly Func<int, int, Bgra> s_line = (x, _) => x == 32 ? s_ink : s_paper;

    [Fact]
    public void Bold_WidensTheStroke_UnderAnOwnScheme()
    {
        var effects = Scheme(s_twoColors);
        var off = Ink(Render(s_line, TextEnhancement.Off, effects));
        var bold = Ink(Render(s_line, new TextEnhancement(0, 2), effects));

        Assert.True(bold >= off + 3, $"stroke {bold} px wide with bold, {off} px without");
    }

    [Fact]
    public void FullStrength_NoHalo_UnderAnOwnScheme()
    {
        var effects = Scheme(s_twoColors);
        var exact = Range(Render(s_line, TextEnhancement.Off, effects with { Smoothing = Smoothing.Pixel }));
        var on = Range(Render(s_line, new TextEnhancement(1, 3), effects));

        Assert.True(on.Low >= exact.Low - 1 && on.High <= exact.High + 1, $"range {on} with enhancement, ink to paper {exact}");
    }

    [Fact]
    public void Replacement_StillRuns_WithEnhancement()
    {
        var replacing = new CustomScheme
        {
            Id = FilterId.Custom2, Kind = SchemeKind.ReplaceOnly,
            Replacements = [new ColorReplacement { From = "#FF0000", To = "#FFFF00", Tolerance = 0 }],
        };
        var pixels = Render((x, _) => x < 32 ? TestImage.Red : s_white, new TextEnhancement(1, 1), Scheme(replacing));

        // Source x = 20 lies deep inside the red area: flat, so sharpness and bold leave it alone.
        var inside = pixels.At((int)((20.5 - Source.X) * Zoom), Target / 2);
        Assert.True(Math.Abs(inside.R - s_yellow.R) <= 2 && Math.Abs(inside.G - s_yellow.G) <= 2 && Math.Abs(inside.B - s_yellow.B) <= 2,
            $"expected yellow, got {inside}");
    }

    private static DisplayEffects Scheme(CustomScheme scheme) =>
        new(new FilterSettings(true, scheme.Id, 1, 0), Smoothing.Text, PointerStyle.Default) { Schemes = [scheme] };

    // The ink is bright green after the scheme: count the bright pixels of the middle row.
    private static int Ink(PixelBuffer pixels) => Row(pixels, Target / 2).Count(luma => luma > 90);
}
