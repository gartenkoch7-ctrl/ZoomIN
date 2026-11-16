using ZoomIN.Core.Filters;

namespace ZoomIN.Core.Tests.Filters;

/// <summary>Matrices of the own schemes (M18, C1–C3): every kind with white, black and red.</summary>
public sealed class SchemeMatricesTests
{
    private const float Tolerance = 0.002f;

    [Fact]
    public void TwoColors_WhiteBecomesColor1_BlackColor2_RedTheMixByItsLuma()
    {
        var m = SchemeMatrices.For(new CustomScheme { Kind = SchemeKind.TwoColors, Color1 = "#00E676", Color2 = "#0D1B4C" });

        AssertColor((0f, 230 / 255f, 118 / 255f), m.Apply(1, 1, 1));
        AssertColor((13 / 255f, 27 / 255f, 76 / 255f), m.Apply(0, 0, 0));

        // Red has luma 0.2126: 21 % of the way from colour 2 to colour 1.
        var (r, g, b) = m.Apply(1, 0, 0);
        AssertColor(Lerp((13, 27, 76), (0, 230, 118), 0.2126f), (r, g, b));
    }

    [Fact]
    public void Tint_WhiteBecomesTheHue_BlackStaysBlack_RedIsADarkHue()
    {
        var m = SchemeMatrices.For(new CustomScheme { Kind = SchemeKind.Tint, Hue = "#FFC107" });

        AssertColor((1f, 193 / 255f, 7 / 255f), m.Apply(1, 1, 1));
        AssertColor((0, 0, 0), m.Apply(0, 0, 0));
        AssertColor((0.2126f, 0.2126f * 193 / 255f, 0.2126f * 7 / 255f), m.Apply(1, 0, 0));
    }

    [Theory]
    [InlineData(ColorDeficiency.Protan)]
    [InlineData(ColorDeficiency.Deutan)]
    [InlineData(ColorDeficiency.Tritan)]
    public void Correction_KeepsWhiteBlackAndGrey(ColorDeficiency deficiency)
    {
        var m = SchemeMatrices.Correction(deficiency, 1);

        AssertColor((1, 1, 1), m.Apply(1, 1, 1));
        AssertColor((0, 0, 0), m.Apply(0, 0, 0));
        AssertColor((0.5f, 0.5f, 0.5f), m.Apply(0.5f, 0.5f, 0.5f));
    }

    [Theory]
    [InlineData(ColorDeficiency.Protan)]
    [InlineData(ColorDeficiency.Deutan)]
    public void RedGreenCorrection_MovesRedTowardsBlue_SoItDiffersFromGreen(ColorDeficiency deficiency)
    {
        var m = SchemeMatrices.Correction(deficiency, 1);

        var (r, g, b) = m.Apply(1, 0, 0);

        // Red keeps its red channel and gains blue, which a red–green deficiency still sees.
        Assert.Equal(1, r, Tolerance);
        Assert.True(b > 0.2f, $"blue {b}");
        Assert.True(g < 0.6f, $"green {g}");
    }

    [Fact]
    public void TritanCorrection_MovesBlueIntoRedAndGreen()
    {
        var (r, g, b) = SchemeMatrices.Correction(ColorDeficiency.Tritan, 1).Apply(0, 0, 1);

        Assert.True(r > 0.1f && g > 0.1f, $"r {r}, g {g}");
        Assert.Equal(1, b, Tolerance);
    }

    [Theory]
    [InlineData(ColorDeficiency.Protan)]
    [InlineData(ColorDeficiency.Deutan)]
    [InlineData(ColorDeficiency.Tritan)]
    public void StrengthZero_IsTheIdentity(ColorDeficiency deficiency)
    {
        var scheme = new CustomScheme { Kind = SchemeKind.Deficiency, Deficiency = deficiency, Strength = 0 };

        Assert.Equal(ColorMatrix.Identity, SchemeMatrices.For(scheme));
    }

    [Fact]
    public void Strength_MixesLinearlyWithTheIdentity()
    {
        var full = SchemeMatrices.Correction(ColorDeficiency.Deutan, 1);
        var seventy = SchemeMatrices.For(new CustomScheme { Kind = SchemeKind.Deficiency, Deficiency = ColorDeficiency.Deutan, Strength = 70 });

        Assert.Equal(1 + (0.7f * (full.M00 - 1)), seventy.M00, 0.0001f);
        Assert.Equal(0.7f * full.M10, seventy.M10, 0.0001f);
    }

    [Fact]
    public void ReplaceOnly_IsTheIdentity()
    {
        Assert.Equal(ColorMatrix.Identity, SchemeMatrices.For(new CustomScheme { Kind = SchemeKind.ReplaceOnly }));
    }

    [Fact]
    public void ColorFilters_UseTheSchemeOfThePlace_AndInvertWithoutOne()
    {
        IReadOnlyList<CustomScheme> schemes = [new CustomScheme { Id = FilterId.Custom3, Kind = SchemeKind.Tint, Hue = "#FF0000" }];

        AssertColor((1, 0, 0), ColorFilters.For(FilterId.Custom3, schemes).Apply(1, 1, 1));
        Assert.Equal(ColorFilters.For(FilterId.Inverted), ColorFilters.For(FilterId.Custom1, schemes));
        Assert.Equal(ColorFilters.For(FilterId.Inverted), ColorFilters.For(FilterId.Custom3));
        Assert.Equal(ColorFilters.For(FilterId.Grayscale), ColorFilters.For(FilterId.Grayscale, schemes));
    }

    [Fact]
    public void Build_AddsContrastAndBrightness_OnlyWhenEnabled()
    {
        IReadOnlyList<CustomScheme> schemes = [new CustomScheme { Id = FilterId.Custom1, Kind = SchemeKind.Tint, Hue = "#FF0000" }];

        AssertColor((1, 0, 0), ColorFilters.Build(new FilterSettings(true, FilterId.Custom1, 1, 0), schemes).Apply(1, 1, 1));
        AssertColor((1, 1, 1), ColorFilters.Build(new FilterSettings(false, FilterId.Custom1, 1, 0), schemes).Apply(1, 1, 1));
    }

    private static (float R, float G, float B) Lerp((float R, float G, float B) a, (float R, float G, float B) b, float t) =>
        ((a.R + (t * (b.R - a.R))) / 255, (a.G + (t * (b.G - a.G))) / 255, (a.B + (t * (b.B - a.B))) / 255);

    private static void AssertColor((float R, float G, float B) expected, (float R, float G, float B) actual)
    {
        Assert.Equal(expected.R, actual.R, Tolerance);
        Assert.Equal(expected.G, actual.G, Tolerance);
        Assert.Equal(expected.B, actual.B, Tolerance);
    }
}
