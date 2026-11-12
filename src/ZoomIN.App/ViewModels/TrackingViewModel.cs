using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using ZoomIN.App.Settings;
using ZoomIN.Core.Tracking;

namespace ZoomIN.App.ViewModels;

/// <summary>Focus, caret and mouse tracking options (M6, mouse follow M17 A1). Applies live.</summary>
public sealed partial class TrackingViewModel : ObservableObject
{
    private readonly TrackingModel _tracking;
    private readonly ILogger _log;
    private readonly bool _ready;
    private bool _restoring;

    public TrackingViewModel(TrackingModel tracking, ILogger<TrackingViewModel> log)
    {
        _tracking = tracking;
        _log = log;
        var o = tracking.Options;
        TrackingEnabled = o.Enabled;
        Focus = o.Focus;
        Caret = o.Caret;
        PanMs = Math.Round(o.PanSeconds * 1000);
        Margin = Math.Round(o.CaretMargin * 100);
        FocusFrame = o.FocusFrame;
        CaretMarker = o.CaretMarker;
        MouseFollowIndex = (int)o.MouseFollow;
        MouseMargin = Math.Round(o.MouseMargin * 100);
        _ready = true;
    }

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
        Enum.IsDefined((MouseFollowMode)MouseFollowIndex) ? (MouseFollowMode)MouseFollowIndex : _tracking.Options.MouseFollow;

    partial void OnTrackingEnabledChanged(bool value) => Apply();

    partial void OnFocusChanged(bool value) => Apply();

    partial void OnCaretChanged(bool value) => Apply();

    partial void OnPanMsChanged(double value) => Apply();

    partial void OnMarginChanged(double value) => Apply();

    partial void OnFocusFrameChanged(bool value) => Apply();

    partial void OnCaretMarkerChanged(bool value) => Apply();

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

    private void Apply()
    {
        if (_ready)
        {
            _tracking.SetOptions(new TrackingOptions(
                TrackingEnabled, Focus, Caret, PanMs / 1000, Margin / 100, FocusFrame, CaretMarker, MouseFollow, MouseMargin / 100));
        }
    }
}
