using ZoomIN.Core.Filters;

namespace ZoomIN.Core.Tests.Filters;

public sealed class EffectsModelTests
{
    private const string Display1 = @"\\.\DISPLAY1";
    private const string Display2 = @"\\.\DISPLAY2";

    private static readonly DisplayEffects s_shared = DisplayEffects.Default with { PerMonitorFilter = false };

    [Fact]
    public void NextFilter_Shared_CyclesAndEnables()
    {
        var model = new EffectsModel(s_shared);

        model.NextFilter(Display1);

        Assert.True(model.Current.Filter.Enabled);
        Assert.Equal(FilterId.InvertedSmart, model.Current.Filter.Filter);
        Assert.Same(model.Current, model.CurrentFor(Display2));
    }

    [Fact]
    public void NextFilter_WrapsAroundToFirst()
    {
        var model = new EffectsModel(s_shared with { Filter = FilterSettings.Default with { Filter = FilterId.WhiteOnBlue } });

        model.NextFilter(Display1);

        Assert.Equal(FilterId.Inverted, model.Current.Filter.Filter);
    }

    [Fact]
    public void ToggleFilter_Shared_FlipsEnabled_AndRaisesChanged()
    {
        var model = new EffectsModel(s_shared);
        var raised = 0;
        model.Changed += () => raised++;

        model.ToggleFilter(Display1);
        Assert.True(model.CurrentFor(Display2).Filter.Enabled);
        model.ToggleFilter(Display1);

        Assert.False(model.Current.Filter.Enabled);
        Assert.Equal(2, raised);
    }

    [Fact]
    public void PerMonitor_IsDefault()
    {
        Assert.True(DisplayEffects.Default.PerMonitorFilter);
    }

    [Fact]
    public void ToggleFilter_PerMonitor_ChangesOnlyThatMonitor()
    {
        var model = new EffectsModel(DisplayEffects.Default);

        model.ToggleFilter(Display1);

        Assert.True(model.CurrentFor(Display1).Filter.Enabled);
        Assert.False(model.CurrentFor(Display2).Filter.Enabled);
        Assert.False(model.Current.Filter.Enabled);
        Assert.Equal(new MonitorFilter(true, FilterId.Inverted), model.Current.MonitorFilters[Display1]);
    }

    [Fact]
    public void NextFilter_PerMonitor_StartsFromSharedFilter()
    {
        var shared = DisplayEffects.Default with { Filter = FilterSettings.Default with { Enabled = true, Filter = FilterId.Grayscale } };
        var model = new EffectsModel(shared);

        model.NextFilter(Display2);

        Assert.Equal(FilterId.GrayscaleInverted, model.CurrentFor(Display2).Filter.Filter);
        Assert.Equal(FilterId.Grayscale, model.CurrentFor(Display1).Filter.Filter);
    }

    [Fact]
    public void CurrentFor_MonitorWithOwnFilter_HasItsMatrix_AndSharedAdjustments()
    {
        var model = new EffectsModel(DisplayEffects.Default);
        model.SetFilter(FilterSettings.Default with { Contrast = 2.0 });

        model.SetMonitorFilter(Display1, new MonitorFilter(true, FilterId.YellowOnBlack));

        var own = model.CurrentFor(Display1);
        Assert.Equal(2.0, own.Filter.Contrast);
        Assert.Equal(ColorFilters.Build(own.Filter), own.Matrix);
        Assert.NotEqual(model.Current.Matrix, own.Matrix);
        Assert.Same(own, model.CurrentFor(Display1));
    }

    [Fact]
    public void PerMonitorOff_AllMonitorsShowShared_EntriesKept()
    {
        var model = new EffectsModel(DisplayEffects.Default);
        model.ToggleFilter(Display1);

        model.SetPerMonitorFilter(false);

        Assert.False(model.CurrentFor(Display1).Filter.Enabled);
        Assert.Equal(new MonitorFilter(false, FilterId.Inverted), model.Current.FilterOf(Display1));

        model.SetPerMonitorFilter(true);

        Assert.True(model.CurrentFor(Display1).Filter.Enabled);
    }

    [Fact]
    public void FilterOf_WithoutOwnEntry_ReturnsShared()
    {
        var effects = DisplayEffects.Default with { Filter = FilterSettings.Default with { Enabled = true, Filter = FilterId.WhiteOnBlue } };

        Assert.Equal(new MonitorFilter(true, FilterId.WhiteOnBlue), effects.FilterOf(Display1));
        Assert.False(effects.HasOwnFilter(Display1, out _));
    }

    [Fact]
    public void Replace_TakesOverMonitorFilters()
    {
        var model = new EffectsModel(DisplayEffects.Default);
        var profile = DisplayEffects.Default with
        {
            MonitorFilters = new Dictionary<string, MonitorFilter> { [Display2] = new(true, FilterId.BlackOnYellow) },
        };

        model.Replace(profile);

        Assert.Equal(FilterId.BlackOnYellow, model.CurrentFor(Display2).Filter.Filter);
        Assert.True(model.CurrentFor(Display2).Filter.Enabled);
        Assert.False(model.CurrentFor(Display1).Filter.Enabled);
    }

    [Fact]
    public void Replace_NullMonitorFilters_BecomesEmpty()
    {
        var model = new EffectsModel(DisplayEffects.Default with { MonitorFilters = null! });

        Assert.Empty(model.Current.MonitorFilters);
        Assert.False(model.CurrentFor(Display1).Filter.Enabled);
    }
}
