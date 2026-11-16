using ZoomIN.Core.Filters;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Core.Settings;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Core.Tests.SecureDesktop;

public sealed class SecureWindowFilterFallbackTests
{
    private const string Display1 = @"\\.\DISPLAY1";
    private const string Display2 = @"\\.\DISPLAY2";

    [Fact]
    public void FilterPerWindowOn_MonitorFilterOff_AutomaticFilterCoversTheMonitor()
    {
        var result = SecureSettingsSanitizer.Sanitize(new AppSettings
        {
            FilterEnabled = false,
            ColorFilter = FilterId.Inverted,
            MonitorFilters = new Dictionary<string, MonitorFilter> { [Display1] = new(false, FilterId.InvertedKeepHue) },
            WindowFilter = WindowFilterOptions.Default with { AutoFilter = FilterId.InvertedSmart },
        });

        Assert.True(result.FilterEnabled);
        Assert.Equal(FilterId.InvertedSmart, result.ColorFilter);
        Assert.Equal(new MonitorFilter(true, FilterId.InvertedSmart), result.MonitorFilters[Display1]);
        Assert.False(result.WindowFilter.Enabled);
    }

    [Fact]
    public void FiltersTheUserSwitchedOn_StayAsTheyAre()
    {
        var result = SecureSettingsSanitizer.Sanitize(new AppSettings
        {
            FilterEnabled = true,
            ColorFilter = FilterId.YellowOnBlack,
            MonitorFilters = new Dictionary<string, MonitorFilter>
            {
                [Display1] = new(true, FilterId.Grayscale),
                [Display2] = new(false, FilterId.Inverted),
            },
            WindowFilter = WindowFilterOptions.Default with { AutoFilter = FilterId.InvertedKeepHue },
        });

        Assert.Equal(FilterId.YellowOnBlack, result.ColorFilter);
        Assert.Equal(new MonitorFilter(true, FilterId.Grayscale), result.MonitorFilters[Display1]);
        Assert.Equal(new MonitorFilter(true, FilterId.InvertedKeepHue), result.MonitorFilters[Display2]);
    }

    [Fact]
    public void FilterPerWindowOff_NothingChanges()
    {
        var result = SecureSettingsSanitizer.Sanitize(new AppSettings
        {
            FilterEnabled = false,
            MonitorFilters = new Dictionary<string, MonitorFilter> { [Display1] = new(false, FilterId.Inverted) },
            WindowFilter = WindowFilterOptions.Default with { Enabled = false },
        });

        Assert.False(result.FilterEnabled);
        Assert.False(result.MonitorFilters[Display1].Enabled);
    }

    [Fact]
    public void UnknownAutomaticFilter_FallsBackToSmartInversion()
    {
        var result = SecureWindowFilterFallback.Apply(
            new AppSettings { FilterEnabled = false },
            WindowFilterOptions.Default with { AutoFilter = (FilterId)999 });

        Assert.True(result.FilterEnabled);
        Assert.Equal(FilterId.InvertedSmart, result.ColorFilter);
    }

    [Fact]
    public void OldFactoryInverted_IsMigratedToSmartInversion_LikeInTheTrayApp()
    {
        var result = SecureWindowFilterFallback.Apply(
            new AppSettings { FilterEnabled = false },
            new WindowFilterOptions { AutoFilter = FilterId.Inverted, AutoFilterRevision = 0 });

        Assert.Equal(FilterId.InvertedSmart, result.ColorFilter);
    }

    [Fact]
    public void MissingFilterPerWindow_NothingChanges()
    {
        var settings = new AppSettings { FilterEnabled = false };

        Assert.Same(settings, SecureWindowFilterFallback.Apply(settings, null));
    }
}
