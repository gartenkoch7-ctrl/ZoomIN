using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Settings;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Core.Tests.Filters;

/// <summary>Deleting an own scheme: everything that chose it falls back to "Inverted" (M18 decision).</summary>
public sealed class SchemeRemovalTests
{
    private const string Display1 = @"\\.\DISPLAY1";
    private const string Display2 = @"\\.\DISPLAY2";
    private static readonly WindowKind s_word = new("winword.exe", "OpusApp");
    private static readonly WindowKind s_excel = new("excel.exe", "XLMAIN");
    private static readonly WindowKind s_edge = new("msedge.exe", "Chrome_WidgetWin_1");

    private static readonly IReadOnlyList<CustomScheme> s_schemes =
    [
        new CustomScheme { Id = FilterId.Custom1 },
        new CustomScheme { Id = FilterId.Custom2 },
    ];

    [Fact]
    public void Effects_SharedAndMonitorFilters_FallBack_OtherSchemesStay()
    {
        var effects = new DisplayEffects(new FilterSettings(true, FilterId.Custom2, 1.2, 0), Smoothing.Text, PointerStyle.Default)
        {
            Schemes = s_schemes,
            MonitorFilters = new Dictionary<string, MonitorFilter>
            {
                [Display1] = new(true, FilterId.Custom2),
                [Display2] = new(false, FilterId.Custom1),
            },
        };

        var after = SchemeRemoval.Without(effects, FilterId.Custom2, out var fallbacks);

        Assert.Equal(2, fallbacks);
        Assert.Equal(new FilterSettings(true, FilterId.Inverted, 1.2, 0), after.Filter);
        Assert.Equal(new MonitorFilter(true, FilterId.Inverted), after.MonitorFilters[Display1]);
        Assert.Equal(new MonitorFilter(false, FilterId.Custom1), after.MonitorFilters[Display2]);
        Assert.Equal([FilterId.Custom1], after.Schemes.Select(s => s.Id));
        Assert.Equal(ColorFilters.Build(after.Filter), after.Matrix);
    }

    [Fact]
    public void WindowOptions_RulesAndAutomaticFilter_FallBack_CountingEveryStoredChoice()
    {
        var options = WindowFilterOptions.Default with
        {
            AutoFilter = FilterId.Custom1,
            Rules =
            [
                WindowFilterRule.For(s_word, WindowFilterChoice.Filter, FilterId.Custom1),
                WindowFilterRule.For(s_excel, WindowFilterChoice.Filter, FilterId.Custom2),
                WindowFilterRule.For(s_edge, WindowFilterChoice.None, FilterId.Custom1) with { TitleBar = WindowTitleBarChoice.Never },
            ],
        };

        var after = SchemeRemoval.Without(options, FilterId.Custom1, out var fallbacks);

        Assert.Equal(3, fallbacks);
        Assert.Equal(FilterId.Inverted, after.AutoFilter);
        Assert.Equal([FilterId.Inverted, FilterId.Custom2, FilterId.Inverted], after.Rules.Select(r => r.Filter));
        Assert.Equal(WindowTitleBarChoice.Never, after.Rules[2].TitleBar);
        Assert.Equal(WindowFilterChoice.None, after.Rules[2].Choice);
    }

    [Fact]
    public void StoredProfile_FallsBack()
    {
        var profile = new AppSettings
        {
            ColorFilter = FilterId.Custom1,
            CustomSchemes = s_schemes,
            MonitorFilters = new Dictionary<string, MonitorFilter> { [Display1] = new(true, FilterId.Custom1) },
        };

        var after = SchemeRemoval.Without(profile, FilterId.Custom1, out var fallbacks);

        Assert.Equal(2, fallbacks);
        Assert.Equal(FilterId.Inverted, after.ColorFilter);
        Assert.Equal(FilterId.Inverted, after.MonitorFilters[Display1].Filter);
    }

    [Fact]
    public void ProfileFile_LeavesTheSharedSchemesOut_KeepsTheChoice()
    {
        var settings = new AppSettings { ColorFilter = FilterId.Custom2, CustomSchemes = s_schemes };

        var file = ProfileFields.ForProfileFile(settings);

        Assert.Empty(file.CustomSchemes);
        Assert.Equal(FilterId.Custom2, file.ColorFilter);
        Assert.Equal(2, settings.CustomSchemes.Count);
    }

    [Fact]
    public void NothingChoseIt_NoFallbacks()
    {
        var profile = new AppSettings { ColorFilter = FilterId.Grayscale, MonitorFilters = null! };

        var after = SchemeRemoval.Without(profile, FilterId.Custom3, out var fallbacks);

        Assert.Equal(0, fallbacks);
        Assert.Equal(FilterId.Grayscale, after.ColorFilter);
        Assert.Empty(after.MonitorFilters);
    }
}
