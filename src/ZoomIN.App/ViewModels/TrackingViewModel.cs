using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using ZoomIN.App.Markers;
using ZoomIN.Core.Localization;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Tracking;

namespace ZoomIN.App.ViewModels;

/// <summary>Focus and caret tracking options (M6) and the caret marker look (E2). Applies live.</summary>
public sealed partial class TrackingViewModel : ObservableObject
{
    private readonly TrackingModel _tracking;
    private readonly ILogger _log;
    private readonly ILocalizer _texts;
    private readonly bool _ready;
    private CaretMarkerStyle _style;

    public TrackingViewModel(TrackingModel tracking, ILocalizer texts, ILogger<TrackingViewModel> log)
    {
        _tracking = tracking;
        _texts = texts;
        _log = log;
        var o = tracking.Options;
        _style = o.CaretStyle;
        TrackingEnabled = o.Enabled;
        Focus = o.Focus;
        Caret = o.Caret;
        PanMs = Math.Round(o.PanSeconds * 1000);
        Margin = Math.Round(o.CaretMargin * 100);
        FocusFrame = o.FocusFrame;
        CaretMarker = o.CaretMarker;
        CenterMouse = o.CenterMouse;
        CaretShapeIndex = (int)o.CaretStyle.Shape;
        CaretColor = o.CaretStyle.Color.ToHex();
        CaretThicknessIndex = (int)o.CaretStyle.Thickness;
        FocusFrameShowIndex = (int)o.FocusFrameShow;
        CaretShowIndex = (int)o.CaretShow;
        _ready = true;
    }

    /// <summary>Raised after the caret marker look changed; the page redraws its preview.</summary>
    public event Action? CaretStyleChanged;

    /// <summary>The caret marker look as chosen on the page.</summary>
    public CaretMarkerStyle CaretStyle => _style;

    /// <summary>What the preview shows, in words, for screen readers.</summary>
    public string CaretDescription => Describe(_style);

    [ObservableProperty]
    public partial bool TrackingEnabled { get; set; }

    [ObservableProperty]
    public partial bool Focus { get; set; }

    [ObservableProperty]
    public partial bool Caret { get; set; }

    [ObservableProperty]
    public partial double PanMs { get; set; }

    [ObservableProperty]
    public partial double Margin { get; set; }

    [ObservableProperty]
    public partial bool FocusFrame { get; set; }

    [ObservableProperty]
    public partial bool CaretMarker { get; set; }

    [ObservableProperty]
    public partial bool CenterMouse { get; set; }

    [ObservableProperty]
    public partial int CaretShapeIndex { get; set; }

    [ObservableProperty]
    public partial string CaretColor { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int CaretThicknessIndex { get; set; }

    /// <summary>When the focus frame is shown (E1).</summary>
    [ObservableProperty]
    public partial int FocusFrameShowIndex { get; set; }

    /// <summary>When the caret marker is shown (E1).</summary>
    [ObservableProperty]
    public partial int CaretShowIndex { get; set; }

    partial void OnTrackingEnabledChanged(bool value) => Apply();

    partial void OnFocusChanged(bool value) => Apply();

    partial void OnCaretChanged(bool value) => Apply();

    partial void OnPanMsChanged(double value) => Apply();

    partial void OnMarginChanged(double value) => Apply();

    partial void OnFocusFrameChanged(bool value) => Apply();

    partial void OnCaretMarkerChanged(bool value)
    {
        Apply();
        if (_ready)
        {
            // The preview shows the marker only while it is switched on.
            OnPropertyChanged(nameof(CaretDescription));
            CaretStyleChanged?.Invoke();
        }
    }

    partial void OnCenterMouseChanged(bool value) => Apply();

    // A ComboBox reports index −1 (no selection) while it unloads: not a choice, nothing to apply.
    partial void OnCaretShapeIndexChanged(int value)
    {
        if (value >= 0)
        {
            Apply();
        }
    }

    partial void OnCaretColorChanged(string value) => Apply();

    partial void OnFocusFrameShowIndexChanged(int value)
    {
        if (value >= 0)
        {
            Apply();
        }
    }

    partial void OnCaretShowIndexChanged(int value)
    {
        if (value >= 0)
        {
            Apply();
        }
    }

    partial void OnCaretThicknessIndexChanged(int value)
    {
        if (value >= 0)
        {
            Apply();
        }
    }

    private void Apply()
    {
        if (!_ready)
        {
            return;
        }

        // Values that cannot be used (no selection, unreadable colour) keep the current look.
        var before = _style;
        _style = CaretMarkerStyle.From((CaretMarkerShape)CaretShapeIndex, CaretColor, (CaretMarkerThickness)CaretThicknessIndex, before);
        var current = _tracking.Options;
        _tracking.SetOptions(new TrackingOptions(
            TrackingEnabled, Focus, Caret, PanMs / 1000, Margin / 100, FocusFrame, CaretMarker, CenterMouse)
        {
            CaretStyle = _style,
            FocusFrameShow = Condition(FocusFrameShowIndex, current.FocusFrameShow),
            CaretShow = Condition(CaretShowIndex, current.CaretShow),
        });
        if (before.Shape != _style.Shape || before.Thickness != _style.Thickness)
        {
            _log.CaretStyleChanged(_style.Shape, _style.Thickness);
        }

        if (before != _style)
        {
            OnPropertyChanged(nameof(CaretDescription));
            CaretStyleChanged?.Invoke();
        }
    }

    private static ShowCondition Condition(int index, ShowCondition current) =>
        index >= 0 && Enum.IsDefined((ShowCondition)index) ? (ShowCondition)index : current;

    private string Describe(CaretMarkerStyle style)
    {
        if (!CaretMarker)
        {
            return _texts.Get("CaretMarker.PreviewOff");
        }

        var color = RgbColor.PresetName(style.Color) is { } name ? _texts.Get("Color." + name) : style.Color.ToHex();
        return _texts.Format(
            "CaretMarker.Preview", _texts.Get("CaretShape." + style.Shape), color, _texts.Get("CaretThickness." + style.Thickness));
    }
}
