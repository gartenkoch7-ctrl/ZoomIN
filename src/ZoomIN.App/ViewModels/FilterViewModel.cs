using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Localization;
using ZoomIN.App.Views;
using ZoomIN.Core.Filters;

namespace ZoomIN.App.ViewModels;

/// <summary>Colour filter, contrast and brightness. Applies live.</summary>
public sealed partial class FilterViewModel : ObservableObject
{
    private readonly PageContext _context;
    private readonly bool _ready;

    public FilterViewModel(PageContext context)
    {
        _context = context;
        FilterNames = Enum.GetValues<ColorFilter>().Select(f => Text.Get("Filter." + f)).ToList();
        var filter = context.Controller.Effects.Current.Filter;
        FilterOn = filter.Enabled;
        FilterIndex = (int)filter.Filter;
        Contrast = Math.Round(filter.Contrast * 100);
        Brightness = Math.Round(filter.Brightness * 100);
        _ready = true;
    }

    public IReadOnlyList<string> FilterNames { get; }

    [ObservableProperty]
    public partial bool FilterOn { get; set; }

    [ObservableProperty]
    public partial int FilterIndex { get; set; }

    [ObservableProperty]
    public partial double Contrast { get; set; }

    [ObservableProperty]
    public partial double Brightness { get; set; }

    public void Reset()
    {
        Contrast = 100;
        Brightness = 0;
    }

    partial void OnFilterOnChanged(bool value) => Apply();

    partial void OnFilterIndexChanged(int value) => Apply();

    partial void OnContrastChanged(double value) => Apply();

    partial void OnBrightnessChanged(double value) => Apply();

    private void Apply()
    {
        if (_ready && FilterIndex >= 0)
        {
            _context.Controller.Effects.SetFilter(new FilterSettings(FilterOn, (ColorFilter)FilterIndex, Contrast / 100, Brightness / 100));
        }
    }
}
