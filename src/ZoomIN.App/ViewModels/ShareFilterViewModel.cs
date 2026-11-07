using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Localization;
using ZoomIN.App.Views;
using ZoomIN.Core.Filters;

namespace ZoomIN.App.ViewModels;

/// <summary>Share filter (ADR-0018): on/off, automatic Teams detection and which filter. Applies live.</summary>
public sealed partial class ShareFilterViewModel : ObservableObject
{
    private readonly PageContext _context;
    private readonly bool _loading = true;

    public ShareFilterViewModel(PageContext context)
    {
        _context = context;
        var options = context.Controller.Share.Options;
        FilterNames = Enum.GetValues<ColorFilter>().Select(f => Text.Get("Filter." + f)).ToList();
        Enabled = options.Enabled;
        AutoDetect = options.AutoDetect;
        FilterIndex = (int)options.Filter;
        _loading = false;
    }

    public IReadOnlyList<string> FilterNames { get; }

    [ObservableProperty]
    public partial bool Enabled { get; set; }

    [ObservableProperty]
    public partial bool AutoDetect { get; set; }

    [ObservableProperty]
    public partial int FilterIndex { get; set; }

    partial void OnEnabledChanged(bool value) => Apply();

    partial void OnAutoDetectChanged(bool value) => Apply();

    partial void OnFilterIndexChanged(int value) => Apply();

    private void Apply()
    {
        if (_loading || FilterIndex < 0)
        {
            return;
        }

        var share = _context.Controller.Share;
        share.SetOptions(share.Options with { Enabled = Enabled, AutoDetect = AutoDetect, Filter = (ColorFilter)FilterIndex });
    }
}
