using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.Core.Tracking;

namespace ZoomIN.App.ViewModels;

/// <summary>Focus and caret tracking options (M6). Applies live.</summary>
public sealed partial class TrackingViewModel : ObservableObject
{
    private readonly TrackingModel _tracking;
    private readonly bool _ready;

    public TrackingViewModel(TrackingModel tracking)
    {
        _tracking = tracking;
        var o = tracking.Options;
        TrackingEnabled = o.Enabled;
        Focus = o.Focus;
        Caret = o.Caret;
        PanMs = Math.Round(o.PanSeconds * 1000);
        Margin = Math.Round(o.CaretMargin * 100);
        FocusFrame = o.FocusFrame;
        CaretMarker = o.CaretMarker;
        CenterMouse = o.CenterMouse;
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

    [ObservableProperty]
    public partial bool CenterMouse { get; set; }

    partial void OnTrackingEnabledChanged(bool value) => Apply();

    partial void OnFocusChanged(bool value) => Apply();

    partial void OnCaretChanged(bool value) => Apply();

    partial void OnPanMsChanged(double value) => Apply();

    partial void OnMarginChanged(double value) => Apply();

    partial void OnFocusFrameChanged(bool value) => Apply();

    partial void OnCaretMarkerChanged(bool value) => Apply();

    partial void OnCenterMouseChanged(bool value) => Apply();

    private void Apply()
    {
        if (_ready)
        {
            _tracking.SetOptions(
                new TrackingOptions(TrackingEnabled, Focus, Caret, PanMs / 1000, Margin / 100, FocusFrame, CaretMarker, CenterMouse));
        }
    }
}
