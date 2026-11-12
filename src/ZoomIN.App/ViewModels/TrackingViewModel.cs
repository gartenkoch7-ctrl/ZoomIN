using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using ZoomIN.App.Markers;
using ZoomIN.App.Settings;
using ZoomIN.Core.Localization;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Tracking;

namespace ZoomIN.App.ViewModels;

/// <summary>Focus, caret and mouse tracking options (M6, mouse follow M17 A1) and the caret marker look (E2, E1). Applies live.</summary>
public sealed partial class TrackingViewModel : ObservableObject
{
    private readonly TrackingModel _tracking;
    private readonly ILogger _log;
    private readonly ILocalizer _texts;
    private readonly bool _ready;
    private CaretMarkerStyle _style;
    private bool _restoring;

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
        MouseFollowIndex = (int)o.MouseFollow;
        MouseMargin = Math.Round(o.MouseMargin * 100);
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

    /// <summary>Order of the combo box = <see cref="MouseFollowMode"/>: middle, margin, fixpoint.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MouseMarginEnabled))]
    public partial int MouseFollowIndex { get; set; }

    /// <summary>Mouse margin in percent (0–50).</summary>
    [ObservableProperty]
    public partial double MouseMargin { get; set; }

    /// <summary>The margin only means something in the margin mode; greyed out otherwise.</summary>
    public bool MouseMarginEnabled => MouseFollow == MouseFollowMode.Margin;

    // The combo box reports -1 while its items are rebuilt; that is no choice of the user.
    private MouseFollowMode MouseFollow =>
        ComboChoice.Or(MouseFollowIndex, _tracking.Options.MouseFollow);

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

    // Every handler changes only its own field (M17 A1, M18 review): the page's other values may be
    // older than the model, e.g. after Strg+Alt+T or a profile switch while the page is open.
    partial void OnTrackingEnabledChanged(bool value) => ApplyOnly(o => o with { Enabled = value });

    partial void OnFocusChanged(bool value) => ApplyOnly(o => o with { Focus = value });

    partial void OnCaretChanged(bool value) => ApplyOnly(o => o with { Caret = value });

    partial void OnPanMsChanged(double value) => ApplyOnly(o => o with { PanSeconds = value / 1000 });

    partial void OnMarginChanged(double value) => ApplyOnly(o => o with { CaretMargin = value / 100 });

    partial void OnFocusFrameChanged(bool value) => ApplyOnly(o => o with { FocusFrame = value });

    partial void OnCaretMarkerChanged(bool value)
    {
        ApplyOnly(o => o with { CaretMarker = value });
        if (_ready)
        {
            // The preview shows the marker only while it is switched on.
            OnPropertyChanged(nameof(CaretDescription));
            CaretStyleChanged?.Invoke();
        }
    }

    partial void OnMouseFollowIndexChanged(int value)
    {
        if (value < 0)
        {
            // The combo box reports -1 while its items are replaced: show the stored mode again,
            // without applying (the page's other values may be older than the model by now).
            if (_ready)
            {
                _restoring = true;
                try
                {
                    MouseFollowIndex = (int)_tracking.Options.MouseFollow;
                }
                finally
                {
                    _restoring = false;
                }
            }

            return;
        }

        if (_restoring)
        {
            return;
        }

        var previous = _tracking.Options.MouseFollow;
        if (_ready && previous != MouseFollow)
        {
            _log.MouseFollowChanged(previous, MouseFollow);
        }

        ApplyOnly(o => o with { MouseFollow = MouseFollow });
    }

    partial void OnMouseMarginChanged(double value) => ApplyOnly(o => o with { MouseMargin = value / 100 });

    // A ComboBox reports index −1 (no selection) while it unloads: ComboChoice keeps the current value.
    partial void OnCaretShapeIndexChanged(int value) => ApplyStyle(s => s with { Shape = ComboChoice.Or(value, s.Shape) });

    // An unreadable colour (while typing) keeps the current one.
    partial void OnCaretColorChanged(string value) => ApplyStyle(s => s with { Color = RgbColor.ParseOrDefault(value, s.Color) });

    partial void OnCaretThicknessIndexChanged(int value) => ApplyStyle(s => s with { Thickness = ComboChoice.Or(value, s.Thickness) });

    partial void OnFocusFrameShowIndexChanged(int value) =>
        ApplyOnly(o => o with { FocusFrameShow = ComboChoice.Or(value, o.FocusFrameShow) });

    partial void OnCaretShowIndexChanged(int value) => ApplyOnly(o => o with { CaretShow = ComboChoice.Or(value, o.CaretShow) });

    private void ApplyOnly(Func<TrackingOptions, TrackingOptions> change)
    {
        if (_ready)
        {
            _tracking.SetOptions(change(_tracking.Options));
        }
    }

    private void ApplyStyle(Func<CaretMarkerStyle, CaretMarkerStyle> change)
    {
        if (!_ready)
        {
            return;
        }

        var before = _style;
        ApplyOnly(o => o with { CaretStyle = change(o.CaretStyle) });
        _style = _tracking.Options.CaretStyle;
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
