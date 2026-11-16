using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Core.Tests.Filters;

/// <summary>Smart inversion (ADR-0024): list order, palette and when the overlay must run.</summary>
public sealed class SmartInvertTests
{
    private static readonly PhysicalRect s_monitor = new(0, 0, 1920, 1080);
    private static readonly DisplayEffects s_keepHueMonitor = Monitor(FilterId.InvertedKeepHue);
    private static readonly DisplayEffects s_smartMonitor = Monitor(FilterId.InvertedSmart);

    [Fact]
    public void DisplayOrder_HasEveryFilterOnce_SmartRightAfterInverted()
    {
        Assert.Equal(Enum.GetValues<FilterId>().Where(f => !CustomSchemes.IsCustom(f)).Order(), ColorFilters.DisplayOrder.Order());
        Assert.Equal(FilterId.InvertedSmart, ColorFilters.DisplayOrder[1]);
        Assert.Equal(1, ColorFilters.IndexIn(ColorFilters.DisplayOrder, FilterId.InvertedSmart));
    }

    [Fact]
    public void SmartMatrix_IsTheInvertedPartKeepingHue()
    {
        Assert.Equal(ColorFilters.For(FilterId.InvertedKeepHue), ColorFilters.For(FilterId.InvertedSmart));
        Assert.Equal(WindowPalette.For(FilterId.InvertedSmart), WindowPalette.Smart);
    }

    [Fact]
    public void IsSmart_OnlyWhenTheSmartFilterIsOn()
    {
        Assert.True(s_smartMonitor.IsSmart);
        Assert.False(s_keepHueMonitor.IsSmart);
        Assert.False((s_smartMonitor with { Filter = s_smartMonitor.Filter with { Enabled = false } }).IsSmart);
    }

    [Fact]
    public void SmartWindow_OnKeepHueMonitor_ChangesTheMonitor_ThoughTheMatrixIsTheSame()
    {
        var regions = Regions(WindowPalette.Smart);

        Assert.True(regions.ChangesMonitor(s_monitor, s_keepHueMonitor));
        Assert.False(regions.ChangesMonitor(s_monitor, s_smartMonitor));
        Assert.True(Regions(WindowPalette.For(FilterId.InvertedKeepHue)).ChangesMonitor(s_monitor, s_smartMonitor));
    }

    [Fact]
    public void UsesSmart_OnlyForSmartRegionsOnTheMonitor()
    {
        Assert.True(Regions(WindowPalette.Smart).UsesSmart(s_monitor));
        Assert.False(Regions(WindowPalette.For(FilterId.Inverted)).UsesSmart(s_monitor));
        Assert.False(Regions(WindowPalette.Smart).UsesSmart(new PhysicalRect(1920, 0, 1920, 1080)));
    }

    [Fact]
    public void NextFilter_GoesFromInvertedToSmart()
    {
        var model = new EffectsModel(DisplayEffects.Default with { PerMonitorFilter = false });

        model.NextFilter("m");

        Assert.Equal(FilterId.InvertedSmart, model.Current.Filter.Filter);
    }

    [Fact]
    public void ProtectPhotos_IsOnFromTheStart_AndCanBeSwitchedOff()
    {
        var model = new EffectsModel(DisplayEffects.Default);
        Assert.True(model.Current.ProtectPhotos);

        model.SetProtectPhotos(false);

        Assert.False(model.Current.ProtectPhotos);
        Assert.False(model.CurrentFor("m").ProtectPhotos);
    }

    private static WindowRegions Regions(byte palette) => new([new PhysicalRect(100, 100, 400, 300)], [palette]);

    private static DisplayEffects Monitor(FilterId filter) =>
        new(new FilterSettings(true, filter, 1, 0), Smoothing.Text, PointerStyle.Default);
}
