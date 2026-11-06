using ZoomIN.Core.Filters;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Core.Tests.WindowFilters;

/// <summary>Dark title bars (dark Windows theme) over light pages: measured apart, not inverted with the page.</summary>
public sealed class TitleBarTests
{
    private const int TitleBar = 32;
    private static readonly WindowKind s_settings = new("systemsettings.exe", "Windows.UI.Core.CoreWindow");
    private static readonly WindowKind s_terminal = new("windowsterminal.exe", "CASCADIA_HOSTING_WINDOW_CLASS");
    private static readonly byte s_inverted = WindowPalette.For(ColorFilter.Inverted);
    private static readonly PhysicalRect s_bounds = new(100, 50, 900, 700);

    [Fact]
    public void DarkTitleBar_OverALightWindow_GetsItsOwnRegion_InFrontOfTheWindow()
    {
        var regions = WindowLayout.Build([Window(WindowBrightness.Light, WindowBrightness.Dark)], WindowFilterOptions.Default);

        Assert.Equal(2, regions.Count);
        Assert.Equal(new PhysicalRect(100, 50, 900, TitleBar), regions.Rects[0]);
        Assert.Equal(WindowPalette.Monitor, regions.Palette[0]);
        Assert.Equal(s_bounds, regions.Rects[1]);
        Assert.Equal(s_inverted, regions.Palette[1]);
    }

    [Theory]
    [InlineData(WindowBrightness.Light)]
    [InlineData(WindowBrightness.Unknown)]
    [InlineData(WindowBrightness.Pending)]
    public void LightOrUnmeasuredTitleBar_IsFilteredWithTheWindow(WindowBrightness titleBar)
    {
        var regions = WindowLayout.Build([Window(WindowBrightness.Light, titleBar)], WindowFilterOptions.Default);

        Assert.Equal(1, regions.Count);
        Assert.Equal(s_bounds, regions.Rects[0]);
    }

    [Fact]
    public void DarkWindow_NeedsNoTitleBarRegion()
    {
        var regions = WindowLayout.Build([Window(WindowBrightness.Dark, WindowBrightness.Dark, s_terminal)], WindowFilterOptions.Default);

        Assert.Same(WindowRegions.Empty, regions);
    }

    [Fact]
    public void UserChoice_Never_KeepsEvenALightTitleBarOutOfTheWindowFilter()
    {
        var options = WindowFilterOptions.Default.WithRule(Rule(WindowFilterChoice.Automatic, WindowTitleBarChoice.Never));

        var regions = WindowLayout.Build([Window(WindowBrightness.Light, WindowBrightness.Light)], options);

        Assert.Equal(2, regions.Count);
        Assert.Equal(WindowPalette.Monitor, regions.Palette[0]);
    }

    [Fact]
    public void UserChoice_LikeWindow_FiltersADarkTitleBarWithTheWindow()
    {
        var options = WindowFilterOptions.Default.WithRule(Rule(WindowFilterChoice.Automatic, WindowTitleBarChoice.LikeWindow));

        var regions = WindowLayout.Build([Window(WindowBrightness.Light, WindowBrightness.Dark)], options);

        Assert.Equal(1, regions.Count);
    }

    [Fact]
    public void FixedFilterOfTheUser_AlsoKeepsADarkTitleBarDark()
    {
        var options = WindowFilterOptions.Default.WithRule(Rule(WindowFilterChoice.Filter, WindowTitleBarChoice.Automatic));

        var regions = WindowLayout.Build([Window(WindowBrightness.Dark, WindowBrightness.Dark)], options);

        Assert.Equal(2, regions.Count);
        Assert.Equal(WindowPalette.Monitor, regions.Palette[0]);
        Assert.Equal(s_inverted, regions.Palette[1]);
    }

    [Fact]
    public void None_OrNoTitleBar_OrNotNormal_NeverGetAnExtraRegion()
    {
        var none = WindowFilterOptions.Default.WithRule(Rule(WindowFilterChoice.None, WindowTitleBarChoice.Never));
        var light = Window(WindowBrightness.Light, WindowBrightness.Dark);

        Assert.Equal(TitleBarResolver.Same, TitleBarResolver.Resolve(light, WindowPalette.Original, none));
        Assert.Equal(TitleBarResolver.Same, TitleBarResolver.Resolve(light with { TitleBarHeight = 0 }, s_inverted, WindowFilterOptions.Default));
        Assert.Equal(TitleBarResolver.Same, TitleBarResolver.Resolve(light with { Role = WindowRole.Popup }, s_inverted, WindowFilterOptions.Default));
        Assert.Equal(TitleBarResolver.Same, TitleBarResolver.Resolve(light, s_inverted, WindowFilterOptions.Default with { Enabled = false }));
    }

    [Fact]
    public void WindowAbove_StillCoversTheTitleBarRegionBelow()
    {
        var cover = new TrackedWindow(2, new PhysicalRect(0, 0, 400, 300), s_terminal, WindowRole.Normal, WindowBrightness.Dark, 0, 2);

        var regions = WindowLayout.Build([cover, Window(WindowBrightness.Light, WindowBrightness.Dark)], WindowFilterOptions.Default);

        Assert.Equal(3, regions.Count);
        Assert.Equal(cover.Bounds, regions.Rects[0]); // first rectangle wins in the shader
        Assert.Equal(TitleBar, regions.Rects[1].Height);
    }

    [Fact]
    public void TitleBarRegions_CountTowardsTheLimit()
    {
        var windows = Enumerable.Range(0, 40)
            .Select(i => Window(WindowBrightness.Light, WindowBrightness.Dark) with { Handle = i + 1, Bounds = new PhysicalRect(i * 10, 0, 900, 700) })
            .ToList();

        var regions = WindowLayout.Build(windows, WindowFilterOptions.Default);

        Assert.Equal(WindowRegions.Max, regions.Count);
        Assert.Equal(80 - WindowRegions.Max, regions.Dropped);
    }

    [Fact]
    public void Area_IsTheTopStripOfTheFrame()
    {
        Assert.Equal(new PhysicalRect(100, 50, 900, 32), TitleBarResolver.Area(s_bounds, TitleBar));
        Assert.True(RectMath.IsEmpty(TitleBarResolver.Area(s_bounds, 0)));
    }

    [Fact]
    public void Shortcut_KeepsTheTitleBarChoiceOfThePage()
    {
        var options = WindowFilterOptions.Default.WithRule(Rule(WindowFilterChoice.Automatic, WindowTitleBarChoice.Never));

        var rule = WindowFilterToggle.Next(s_settings, s_inverted, monitorFiltered: false, options);

        Assert.Equal(WindowFilterChoice.None, rule.Choice);
        Assert.Equal(WindowTitleBarChoice.Never, rule.TitleBar);
    }

    [Fact]
    public void Normalized_DropsRulesWithAnUnknownTitleBarChoice()
    {
        var options = WindowFilterOptions.Default with { Rules = [Rule(WindowFilterChoice.Automatic, (WindowTitleBarChoice)42)] };

        Assert.Empty(options.Normalized().Rules);
    }

    private static WindowFilterRule Rule(WindowFilterChoice choice, WindowTitleBarChoice titleBar) =>
        WindowFilterRule.For(s_settings, choice) with { TitleBar = titleBar };

    private static TrackedWindow Window(WindowBrightness brightness, WindowBrightness titleBar, WindowKind? kind = null) =>
        new(1, s_bounds, kind ?? s_settings, WindowRole.Normal, brightness, 0, 1, TitleBar, titleBar);
}
