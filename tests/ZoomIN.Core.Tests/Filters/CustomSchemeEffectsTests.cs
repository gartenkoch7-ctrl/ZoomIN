using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Core.Tests.Filters;

/// <summary>Own schemes in the effects, the palette and "next filter" (M18).</summary>
public sealed class CustomSchemeEffectsTests
{
    private const string Monitor = @"\\.\DISPLAY1";
    private static readonly PhysicalRect s_monitor = new(0, 0, 1920, 1080);

    private static readonly CustomScheme s_amber = new() { Id = FilterId.Custom1, Name = "Bernstein", Kind = SchemeKind.Tint, Hue = "#FFC107" };

    private static readonly CustomScheme s_redToYellow = new()
    {
        Id = FilterId.Custom3,
        Kind = SchemeKind.Deficiency,
        Strength = 70,
        Replacements = [new ColorReplacement { From = "#FF0000", To = "#FFFF00", Tolerance = 10 }],
    };

    [Fact]
    public void Palette_HasFourteenFixedEntries_BuiltInIndexesStayPut()
    {
        Assert.Equal(14, WindowPalette.Count);
        Assert.Equal(2, WindowPalette.For(FilterId.Inverted));
        Assert.Equal(9, WindowPalette.Smart);
        Assert.Equal(10, WindowPalette.For(FilterId.Custom1));
        Assert.Equal(13, WindowPalette.For(FilterId.Custom4));
        Assert.Equal(WindowPalette.FirstScheme, WindowPalette.For(FilterId.Custom1));
        Assert.Equal(2, WindowPalette.SchemeSlotOf(WindowPalette.For(FilterId.Custom3)));
        Assert.Equal(-1, WindowPalette.SchemeSlotOf(WindowPalette.Smart));
        Assert.Equal("Custom2", WindowPalette.Name(WindowPalette.For(FilterId.Custom2)));
    }

    [Fact]
    public void Effects_TakeTheSchemeMatrices_ForMonitorAndPalette()
    {
        var effects = Effects(new FilterSettings(true, FilterId.Custom1, 1, 0));

        Assert.Equal(SchemeMatrices.For(s_amber), effects.Matrix);
        Assert.Equal(SchemeMatrices.For(s_amber), effects.WindowMatrices[WindowPalette.For(FilterId.Custom1)]);
        Assert.Equal(SchemeMatrices.For(s_redToYellow), effects.WindowMatrices[WindowPalette.For(FilterId.Custom3)]);
        Assert.Equal(ColorFilters.For(FilterId.Inverted), effects.WindowMatrices[WindowPalette.For(FilterId.Custom2)]);
        Assert.Equal(WindowPalette.Count, effects.WindowMatrices.Length);
    }

    [Fact]
    public void SettingSchemesOrFilterLater_RecomputesEverything()
    {
        var plain = new DisplayEffects(new FilterSettings(true, FilterId.Custom3, 1, 0), Smoothing.Text, PointerStyle.Default);
        var withSchemes = plain with { Schemes = [s_amber, s_redToYellow] };
        var switched = withSchemes with { Filter = withSchemes.Filter with { Filter = FilterId.Custom1 } };

        Assert.Equal(ColorFilters.For(FilterId.Inverted), plain.Matrix);
        Assert.Equal(-1, plain.MonitorReplaceSlot);
        Assert.Equal(SchemeMatrices.For(s_redToYellow), withSchemes.Matrix);
        Assert.Equal(2, withSchemes.MonitorReplaceSlot);
        Assert.Equal(SchemeMatrices.For(s_amber), switched.Matrix);
        Assert.Equal(-1, switched.MonitorReplaceSlot);
        Assert.Equal(10, switched.Order.Count);
    }

    [Fact]
    public void ReplaceSlotOf_OnlyForEntriesWithReplacements()
    {
        var effects = Effects(new FilterSettings(true, FilterId.Custom3, 1, 0));
        var off = Effects(new FilterSettings(false, FilterId.Custom3, 1, 0));

        Assert.Equal(2, effects.ReplaceSlotOf(WindowPalette.Monitor));
        Assert.Equal(2, effects.ReplaceSlotOf(WindowPalette.For(FilterId.Custom3)));
        Assert.Equal(-1, effects.ReplaceSlotOf(WindowPalette.For(FilterId.Custom1)));
        Assert.Equal(-1, effects.ReplaceSlotOf(WindowPalette.Original));
        Assert.Equal(-1, off.ReplaceSlotOf(WindowPalette.Monitor));
    }

    [Fact]
    public void ReplaceOnlyWindow_ChangesTheMonitor_ThoughItsMatrixIsTheSame()
    {
        var replaceOnly = s_redToYellow with { Kind = SchemeKind.ReplaceOnly };
        var effects = new DisplayEffects(FilterSettings.Default, Smoothing.Text, PointerStyle.Default) { Schemes = [replaceOnly] };
        var regions = new WindowRegions([new PhysicalRect(0, 0, 100, 100)], [WindowPalette.For(FilterId.Custom3)]);
        var monitorAlike = new WindowRegions([new PhysicalRect(0, 0, 100, 100)], [WindowPalette.Original]);

        Assert.Equal(effects.WindowMatrices[WindowPalette.Monitor], effects.WindowMatrices[WindowPalette.For(FilterId.Custom3)]);
        Assert.True(regions.ChangesMonitor(s_monitor, effects));
        Assert.False(monitorAlike.ChangesMonitor(s_monitor, effects));
    }

    [Fact]
    public void NextFilter_CyclesThroughBuiltInFiltersAndOwnSchemes()
    {
        var model = new EffectsModel(Effects(new FilterSettings(true, FilterId.WhiteOnBlue, 1, 0)) with { PerMonitorFilter = false });

        model.NextFilter(Monitor);
        Assert.Equal(FilterId.Custom1, model.Current.Filter.Filter);
        model.NextFilter(Monitor);
        Assert.Equal(FilterId.Custom3, model.Current.Filter.Filter);
        model.NextFilter(Monitor);
        Assert.Equal(FilterId.Inverted, model.Current.Filter.Filter);
    }

    [Fact]
    public void UpdateSchemes_NormalizesAndKeepsTheFilter_PerMonitorCopiesFollow()
    {
        var model = new EffectsModel(Effects(new FilterSettings(true, FilterId.Inverted, 1, 0)));
        model.SetMonitorFilter(Monitor, new MonitorFilter(true, FilterId.Custom2));
        var changed = 0;
        model.Changed += () => changed++;

        model.UpdateSchemes(_ => [s_amber, s_amber with { Id = FilterId.Custom2, Hue = "#FF0000" }, s_amber]);

        Assert.Equal(1, changed);
        Assert.Equal([FilterId.Custom1, FilterId.Custom2], model.Current.Schemes.Select(s => s.Id));
        Assert.Equal(FilterId.Inverted, model.Current.Filter.Filter);
        var (r, g, b) = model.CurrentFor(Monitor).Matrix.Apply(1, 1, 1);
        Assert.Equal(1, r, 0.001f);
        Assert.Equal(0, g + b, 0.001f);
    }

    [Fact]
    public void Mapping_TakesSchemesFromAndBackToTheSettings()
    {
        var settings = new AppSettings { CustomSchemes = [s_redToYellow, s_amber], ColorFilter = FilterId.Custom3 };

        var model = EffectsMapping.Create(settings);
        var back = EffectsMapping.Apply(new AppSettings(), model);

        Assert.Equal([FilterId.Custom1, FilterId.Custom3], model.Current.Schemes.Select(s => s.Id));
        Assert.Equal(FilterId.Custom3, back.ColorFilter);
        Assert.Equal(model.Current.Schemes, back.CustomSchemes);
    }

    private static DisplayEffects Effects(FilterSettings filter) =>
        new(filter, Smoothing.Text, PointerStyle.Default) { Schemes = [s_amber, s_redToYellow] };
}
