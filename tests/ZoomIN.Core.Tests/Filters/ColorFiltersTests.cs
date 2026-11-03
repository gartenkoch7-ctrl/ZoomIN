using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;

namespace ZoomIN.Core.Tests.Filters;

public sealed class ColorFiltersTests
{
    private const float White = 1f;
    private const float Black = 0f;

    [Fact]
    public void Inverted_SwapsBlackAndWhite_AndInvertsRed()
    {
        var m = ColorFilters.For(ColorFilter.Inverted);

        Assert.Equal((Black, Black, Black), m.Apply(White, White, White));
        Assert.Equal((0f, 1f, 1f), m.Apply(1, 0, 0));
    }

    [Fact]
    public void InvertedKeepHue_SwapsBlackAndWhite_ButRedStaysReddish()
    {
        var m = ColorFilters.For(ColorFilter.InvertedKeepHue);

        AssertClose((Black, Black, Black), m.Apply(White, White, White));
        AssertClose((White, White, White), m.Apply(Black, Black, Black));
        var (r, g, b) = m.Apply(1, 0, 0);
        Assert.True(r > g && r > b, $"red should dominate, got ({r}, {g}, {b})");
    }

    [Fact]
    public void Grayscale_MakesChannelsEqual()
    {
        var (r, g, b) = ColorFilters.For(ColorFilter.Grayscale).Apply(0.9f, 0.2f, 0.4f);

        Assert.Equal(r, g, 5);
        Assert.Equal(g, b, 5);
    }

    [Fact]
    public void YellowOnBlack_TurnsWhiteBackgroundBlack_AndBlackTextYellow()
    {
        var m = ColorFilters.For(ColorFilter.YellowOnBlack);

        AssertClose((Black, Black, Black), m.Apply(White, White, White));
        AssertClose((White, White, Black), m.Apply(Black, Black, Black));
    }

    [Fact]
    public void BlackOnYellow_TurnsWhiteBackgroundYellow_AndKeepsBlackText()
    {
        var m = ColorFilters.For(ColorFilter.BlackOnYellow);

        AssertClose((White, White, Black), m.Apply(White, White, White));
        AssertClose((Black, Black, Black), m.Apply(Black, Black, Black));
    }

    [Fact]
    public void WhiteOnBlue_TurnsWhiteBackgroundDarkBlue_AndBlackTextWhite()
    {
        var m = ColorFilters.For(ColorFilter.WhiteOnBlue);

        AssertClose((Black, Black, 0.5f), m.Apply(White, White, White));
        AssertClose((White, White, White), m.Apply(Black, Black, Black));
    }

    [Fact]
    public void Build_Disabled_NeutralContrast_IsIdentity()
    {
        Assert.Equal(ColorMatrix.Identity, ColorFilters.Build(FilterSettings.Default));
    }

    [Fact]
    public void Build_Contrast_SpreadsAroundMidGrey()
    {
        var m = ColorFilters.Build(FilterSettings.Default with { Contrast = 2 });

        AssertClose((0.5f, 0.5f, 0.5f), m.Apply(0.5f, 0.5f, 0.5f));
        AssertClose((0.9f, 0.9f, 0.9f), m.Apply(0.7f, 0.7f, 0.7f));
    }

    [Fact]
    public void DisplayEffects_With_RecomputesMatrix()
    {
        var effects = DisplayEffects.Default with { Filter = FilterSettings.Default with { Enabled = true } };

        Assert.Equal(ColorFilters.For(ColorFilter.Inverted), effects.Matrix);
    }

    [Fact]
    public void PointerStyle_ScaleGrowsWithZoom_UpToMax()
    {
        Assert.Equal(1.0, PointerStyle.Default.ScaleFor(1));
        Assert.Equal(3.0, PointerStyle.Default.ScaleFor(3));
        Assert.Equal(4.0, PointerStyle.Default.ScaleFor(12));
    }

    private static void AssertClose((float R, float G, float B) expected, (float R, float G, float B) actual)
    {
        Assert.Equal(expected.R, actual.R, 0.01f);
        Assert.Equal(expected.G, actual.G, 0.01f);
        Assert.Equal(expected.B, actual.B, 0.01f);
    }
}
