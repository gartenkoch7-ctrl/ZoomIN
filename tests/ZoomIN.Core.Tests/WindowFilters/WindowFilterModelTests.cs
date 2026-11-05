using ZoomIN.Core.Filters;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Core.Tests.WindowFilters;

public sealed class WindowFilterModelTests
{
    private static readonly WindowKind s_word = new("winword.exe", "OpusApp");

    [Fact]
    public void Publish_WakesOnlyOnRealChanges()
    {
        var model = new WindowFilterModel(WindowFilterOptions.Default);
        var changes = 0;
        model.RegionsChanged += () => changes++;

        model.Publish(Regions(100));
        model.Publish(Regions(100));
        model.Publish(Regions(120));
        model.Publish(WindowRegions.Empty);

        Assert.Equal(3, changes);
        Assert.Same(WindowRegions.Empty, model.Regions);
    }

    [Fact]
    public void SetOptions_Normalizes_AndTellsTheTracker()
    {
        var model = new WindowFilterModel(WindowFilterOptions.Default);
        var told = false;
        model.OptionsChanged += () => told = true;

        model.SetOptions(WindowFilterOptions.Default with { CustomPercent = 1000 });

        Assert.True(told);
        Assert.Equal(WindowFilterOptions.MaxPercent, model.Options.CustomPercent);
    }

    [Theory]
    [InlineData(true, false, WindowFilterChoice.None)]
    [InlineData(false, false, WindowFilterChoice.Filter)]
    [InlineData(false, true, WindowFilterChoice.None)]
    public void Toggle_TurnsWhatLooksFilteredOff_AndTheRestOn(bool ownFilter, bool monitorFiltered, WindowFilterChoice expected)
    {
        var palette = ownFilter ? WindowPalette.For(ColorFilter.Inverted) : WindowPalette.Monitor;
        var options = WindowFilterOptions.Default with { AutoFilter = ColorFilter.BlackOnYellow };

        var rule = WindowFilterToggle.Next(s_word, palette, monitorFiltered, options);

        Assert.Equal(expected, rule.Choice);
        Assert.Equal(s_word, rule.Kind);
        if (expected == WindowFilterChoice.Filter)
        {
            Assert.Equal(ColorFilter.BlackOnYellow, rule.Filter);
        }
    }

    [Fact]
    public void Toggle_OnAnOriginalWindowUnderAFilteredMonitor_FiltersIt()
    {
        var rule = WindowFilterToggle.Next(s_word, WindowPalette.Original, monitorFiltered: true, WindowFilterOptions.Default);

        Assert.Equal(WindowFilterChoice.Filter, rule.Choice);
    }

    private static WindowRegions Regions(double width) =>
        new([new PhysicalRect(0, 0, width, 100)], [WindowPalette.For(ColorFilter.Inverted)]);
}
