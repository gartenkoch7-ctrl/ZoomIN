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

    // Only the changed field (M17 A1): the page's other values may be older than the model, e.g.
    // after Strg+Alt+T or a profile switch while the page is open.
    private void ApplyOnly(Func<TrackingOptions, TrackingOptions> change)
    {
        if (_ready)
        {
            _tracking.SetOptions(change(_tracking.Options));
        }
    }

    partial void OnCaretShapeIndexChanged(int value) => ApplyChoice(value);

    partial void OnCaretColorChanged(string value) => Apply();

    partial void OnFocusFrameShowIndexChanged(int value) => ApplyChoice(value);

    partial void OnCaretShowIndexChanged(int value) => ApplyChoice(value);

    partial void OnCaretThicknessIndexChanged(int value) => ApplyChoice(value);

    // A ComboBox reports index −1 (no selection) while it unloads: not a choice, nothing to apply.
    private void ApplyChoice(int index)
    {
        if (index >= 0)
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
            TrackingEnabled, Focus, Caret, PanMs / 1000, Margin / 100, FocusFrame, CaretMarker, MouseFollow, MouseMargin / 100)
        {
            CaretStyle = _style,
            FocusFrameShow = ComboChoice.Or(FocusFrameShowIndex, current.FocusFrameShow),
            CaretShow = ComboChoice.Or(CaretShowIndex, current.CaretShow),
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
