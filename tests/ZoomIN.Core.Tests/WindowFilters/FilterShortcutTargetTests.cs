using ZoomIN.Core.Filters;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Core.Tests.WindowFilters;

/// <summary>ADR-0033: what the colour filter shortcut (Ctrl+Alt+C by default) switches while the smart inversion is on.</summary>
public sealed class FilterShortcutTargetTests
{
    private static readonly WindowKind s_word = new("winword.exe", "OpusApp");

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)] // rules have no effect without the filter per window
    [InlineData(false, true, false)] // any other filter switches the monitor as before
    [InlineData(false, false, false)]
    public void ActsOnWindow_OnlyOnASmartMonitorWithTheFilterPerWindowOn(bool smart, bool windowFilter, bool expected)
    {
        Assert.Equal(expected, FilterShortcutTarget.ActsOnWindow(smart, windowFilter));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(400, true)]
    [InlineData(401, false)]
    [InlineData(-1, false)]
    public void DoublePress_IsTwoPressesWithin400Ms(int milliseconds, bool expected)
    {
        Assert.Equal(expected, FilterShortcutTarget.IsDoublePress(TimeSpan.FromMilliseconds(milliseconds)));
    }

    [Fact]
    public void AFilteredWindow_GetsOriginalColours()
    {
        // On the smart monitor (palette "monitor") or with a smart filter of its own.
        foreach (var palette in new[] { WindowPalette.Monitor, WindowPalette.Smart })
        {
            var rule = FilterShortcutTarget.NextRule(s_word, palette, WindowFilterOptions.Default);

            Assert.Equal(WindowFilterChoice.None, rule.Choice);
            Assert.Equal(s_word, rule.Kind);
        }
    }

    [Fact]
    public void AnOriginalWindow_GoesBackToAutomatic_NotToAFixedFilter()
    {
        var rule = FilterShortcutTarget.NextRule(s_word, WindowPalette.Original, WindowFilterOptions.Default);

        Assert.Equal(WindowFilterChoice.Automatic, rule.Choice);
    }

    [Fact]
    public void AFixedRuleOfThePage_KeepsItsFilter_WhenSwitchedToOriginal()
    {
        var options = WindowFilterOptions.Default.WithRule(
            WindowFilterRule.For(s_word, WindowFilterChoice.Filter, FilterId.Grayscale));

        var rule = FilterShortcutTarget.NextRule(s_word, WindowPalette.For(FilterId.Grayscale), options);

        Assert.Equal(WindowFilterChoice.None, rule.Choice);
        Assert.Equal(FilterId.Grayscale, rule.Filter);
    }

    [Fact]
    public void TheTitleBarChoiceOfThePage_Stays()
    {
        var options = WindowFilterOptions.Default.WithRule(
            WindowFilterRule.For(s_word, WindowFilterChoice.Automatic) with { TitleBar = WindowTitleBarChoice.Never });

        var rule = FilterShortcutTarget.NextRule(s_word, WindowPalette.Monitor, options);

        Assert.Equal(WindowTitleBarChoice.Never, rule.TitleBar);
    }
}
