using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Localization;
using ZoomIN.App.Zoom;
using ZoomIN.Core.Filters;

namespace ZoomIN.App.ViewModels;

/// <summary>
/// Colour filter, contrast and brightness. Applies live. With "own filter per monitor", on/off and
/// kind apply to the monitor picked in the list; entry 0 is the shared filter.
/// </summary>
public sealed partial class FilterViewModel : ObservableObject
{
    private readonly EffectsModel _effects;
    private readonly List<string> _monitorIds;
    private bool _loading = true;

    public FilterViewModel(EffectsModel effects, ZoomCommands zoom)
    {
        _effects = effects;
        var current = effects.Current;
        FilterNames = ColorFilters.DisplayOrder.Select(f => Text.Get("Filter." + f)).ToList();
        _monitorIds = [.. zoom.MonitorIds().Order(StringComparer.OrdinalIgnoreCase)];
        MonitorNames = [Text.Get("FilterMonitor.Shared"), .. _monitorIds.Select(MonitorName)];
        PerMonitor = current.PerMonitorFilter;
        Contrast = Math.Round(current.Filter.Contrast * 100);
        Brightness = Math.Round(current.Filter.Brightness * 100);
        ProtectPhotos = current.ProtectPhotos;

        // Starts on the monitor the user is working on: that is the one they most likely want to change.
        var underMouse = _monitorIds.IndexOf(zoom.MonitorUnderMouse());
        MonitorIndex = PerMonitor && underMouse >= 0 ? underMouse + 1 : 0;
        LoadChoice();
        _loading = false;
    }

    public IReadOnlyList<string> FilterNames { get; }

    public IReadOnlyList<string> MonitorNames { get; }

    [ObservableProperty]
    public partial bool PerMonitor { get; set; }

    [ObservableProperty]
    public partial int MonitorIndex { get; set; }

    [ObservableProperty]
    public partial bool FilterOn { get; set; }

    [ObservableProperty]
    public partial int FilterIndex { get; set; }

    [ObservableProperty]
    public partial double Contrast { get; set; }

    [ObservableProperty]
    public partial double Brightness { get; set; }

    /// <summary>Smart inversion leaves photos and videos original (ADR-0024).</summary>
    [ObservableProperty]
    public partial bool ProtectPhotos { get; set; }

    private string? SelectedMonitor => PerMonitor && MonitorIndex > 0 ? _monitorIds[MonitorIndex - 1] : null;

    public void Reset()
    {
        Contrast = 100;
        Brightness = 0;
    }

    partial void OnPerMonitorChanged(bool value)
    {
        if (!_loading)
        {
            _effects.SetPerMonitorFilter(value);
            Reload();
        }
    }

    partial void OnMonitorIndexChanged(int value) => Reload();

    partial void OnFilterOnChanged(bool value) => ApplyChoice();

    partial void OnFilterIndexChanged(int value) => ApplyChoice();

    partial void OnContrastChanged(double value) => ApplyAdjustment();

    partial void OnBrightnessChanged(double value) => ApplyAdjustment();

    partial void OnProtectPhotosChanged(bool value)
    {
        if (!_loading)
        {
            _effects.SetProtectPhotos(value);
        }
    }

    // "\\.\DISPLAY2" → "Monitor 2", the number Windows shows under Settings › Display.
    private static string MonitorName(string id)
    {
        var number = Core.Monitors.MonitorNames.Number(id);
        return number > 0 ? Text.Format("FilterMonitor.Name", number) : id;
    }

    private void Reload()
    {
        if (_loading)
        {
            return;
        }

        _loading = true;
        LoadChoice();
        _loading = false;
    }

    private void LoadChoice()
    {
        var effects = _effects.Current;
        var choice = SelectedMonitor is { } id ? effects.FilterOf(id) : new MonitorFilter(effects.Filter.Enabled, effects.Filter.Filter);
        FilterOn = choice.Enabled;
        FilterIndex = ColorFilters.DisplayIndexOf(choice.Filter);
    }

    private void ApplyChoice()
    {
        if (_loading || FilterIndex < 0)
        {
            return;
        }

        var effects = _effects;
        var choice = new MonitorFilter(FilterOn, ColorFilters.DisplayOrder[FilterIndex]);
        if (SelectedMonitor is { } id)
        {
            effects.SetMonitorFilter(id, choice);
        }
        else
        {
            effects.SetFilter(effects.Current.Filter with { Enabled = choice.Enabled, Filter = choice.Filter });
        }
    }

    private void ApplyAdjustment()
    {
        if (!_loading)
        {
            var effects = _effects;
            effects.SetFilter(effects.Current.Filter with { Contrast = Contrast / 100, Brightness = Brightness / 100 });
        }
    }
}
