using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Localization;
using ZoomIN.App.Views;
using ZoomIN.Core.Filters;

namespace ZoomIN.App.ViewModels;

/// <summary>Colour filter, contrast, brightness and pointer style. Applies live.</summary>
public sealed partial class ColorsViewModel : ObservableObject
{
    private readonly PageContext _context;
    private readonly bool _ready;

    public ColorsViewModel(PageContext context)
    {
        _context = context;
        FilterNames = Enum.GetValues<ColorFilter>().Select(f => Text.Get("Filter." + f)).ToList();
        var effects = context.Controller.Effects.Current;
        FilterOn = effects.Filter.Enabled;
        FilterIndex = (int)effects.Filter.Filter;
        Contrast = Math.Round(effects.Filter.Contrast * 100);
        Brightness = Math.Round(effects.Filter.Brightness * 100);
        CursorMax = effects.Cursor.MaxScale;
        Rim = effects.Cursor.Rim;
        AutoLocate = effects.Cursor.AutoLocate;
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

    [ObservableProperty]
    public partial double CursorMax { get; set; }

    [ObservableProperty]
    public partial bool Rim { get; set; }

    [ObservableProperty]
    public partial bool AutoLocate { get; set; }

    partial void OnFilterOnChanged(bool value) => ApplyFilter();

    partial void OnFilterIndexChanged(int value) => ApplyFilter();

    partial void OnContrastChanged(double value) => ApplyFilter();

    partial void OnBrightnessChanged(double value) => ApplyFilter();

    partial void OnCursorMaxChanged(double value) => ApplyCursor();

    partial void OnRimChanged(bool value) => ApplyCursor();

    partial void OnAutoLocateChanged(bool value) => ApplyCursor();

    private void ApplyFilter()
    {
        if (_ready && FilterIndex >= 0)
        {
            _context.Controller.Effects.SetFilter(new FilterSettings(FilterOn, (ColorFilter)FilterIndex, Contrast / 100, Brightness / 100));
        }
    }

    private void ApplyCursor()
    {
        if (_ready)
        {
            _context.Controller.Effects.SetCursor(new CursorStyle(CursorMax, Rim, AutoLocate));
        }
    }
}
