using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Localization;
using ZoomIN.App.Views;
using ZoomIN.Core.Filters;

namespace ZoomIN.App.ViewModels;

/// <summary>
/// Colour filter, contrast and brightness. Applies live. With "own filter per monitor", on/off and
/// kind apply to the monitor picked in the list; entry 0 is the shared filter.
/// </summary>
public sealed partial class FilterViewModel : ObservableObject
{
    private readonly PageContext _context;
    private readonly List<string> _monitorIds;
    private bool _loading = true;

    public FilterViewModel(PageContext context)
    {
        _context = context;
        var controller = context.Controller;
        var effects = controller.Effects.Current;
        FilterNames = ColorFilters.DisplayOrder.Select(f => Text.Get("Filter." + f)).ToList();
        _monitorIds = [.. controller.MonitorIds().Order(StringComparer.OrdinalIgnoreCase)];
        MonitorNames = [Text.Get("FilterMonitor.Shared"), .. _monitorIds.Select(MonitorName)];
        PerMonitor = effects.PerMonitorFilter;
        Contrast = Math.Round(effects.Filter.Contrast * 100);
        Brightness = Math.Round(effects.Filter.Brightness * 100);

        // Starts on the monitor the user is working on: that is the one they most likely want to change.
        var underMouse = _monitorIds.IndexOf(controller.MonitorUnderMouse());
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
            _context.Controller.Effects.SetPerMonitorFilter(value);
            Reload();
        }
    }

    partial void OnMonitorIndexChanged(int value) => Reload();

    partial void OnFilterOnChanged(bool value) => ApplyChoice();

    partial void OnFilterIndexChanged(int value) => ApplyChoice();

    partial void OnContrastChanged(double value) => ApplyAdjustment();

    partial void OnBrightnessChanged(double value) => ApplyAdjustment();

    // "\\.\DISPLAY2" → "Monitor 2", the number Windows shows under Settings › Display.
    private static string MonitorName(string id)
    {
        var digits = new string([.. id.Reverse().TakeWhile(char.IsDigit).Reverse()]);
        return digits.Length > 0 ? Text.Format("FilterMonitor.Name", digits) : id;
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
        var effects = _context.Controller.Effects.Current;
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

        var effects = _context.Controller.Effects;
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
            var effects = _context.Controller.Effects;
            effects.SetFilter(effects.Current.Filter with { Contrast = Contrast / 100, Brightness = Brightness / 100 });
        }
    }
}
