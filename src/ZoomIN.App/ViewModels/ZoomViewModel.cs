using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Zoom;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.ViewModels;

/// <summary>Mode, levels per monitor, glide, lens, docked area and smoothing. Applies live.</summary>
public sealed partial class ZoomViewModel : ObservableObject
{
    private readonly ZoomModel _model;
    private readonly EffectsModel _effects;
    private readonly ZoomCommands _zoom;
    private readonly bool _ready;

    public ZoomViewModel(ZoomModel model, EffectsModel effects, ZoomCommands zoom)
    {
        _model = model;
        _effects = effects;
        _zoom = zoom;
        ModeIndex = (int)model.Mode;
        PerMonitor = model.PerMonitor;
        AnimationMs = Math.Round(model.AnimationSeconds * 1000);
        LensWidth = Math.Round(model.Lens.WidthFraction * 100);
        LensHeight = Math.Round(model.Lens.HeightFraction * 100);
        DockEdgeIndex = (int)model.Dock.Edge;
        DockSize = Math.Round(model.Dock.Fraction * 100);
        SmoothingIndex = (int)effects.Current.Smoothing;
        _ready = true;
    }

    [ObservableProperty]
    public partial int ModeIndex { get; set; }

    [ObservableProperty]
    public partial bool PerMonitor { get; set; }

    [ObservableProperty]
    public partial double AnimationMs { get; set; }

    [ObservableProperty]
    public partial double LensWidth { get; set; }

    [ObservableProperty]
    public partial double LensHeight { get; set; }

    [ObservableProperty]
    public partial int DockEdgeIndex { get; set; }

    [ObservableProperty]
    public partial double DockSize { get; set; }

    [ObservableProperty]
    public partial int SmoothingIndex { get; set; }

    partial void OnModeIndexChanged(int value)
    {
        if (_ready && value >= 0)
        {
            _zoom.SelectMode((ZoomMode)value);
        }
    }

    partial void OnPerMonitorChanged(bool value)
    {
        if (_ready)
        {
            _model.PerMonitor = value;
        }
    }

    partial void OnAnimationMsChanged(double value)
    {
        if (_ready)
        {
            _model.AnimationSeconds = value / 1000;
        }
    }

    partial void OnLensWidthChanged(double value) => ApplyLens();

    partial void OnLensHeightChanged(double value) => ApplyLens();

    partial void OnDockEdgeIndexChanged(int value) => ApplyDock();

    partial void OnDockSizeChanged(double value) => ApplyDock();

    partial void OnSmoothingIndexChanged(int value)
    {
        if (_ready && value >= 0)
        {
            _effects.SetSmoothing((Smoothing)value);
        }
    }

    private void ApplyLens()
    {
        if (_ready)
        {
            _model.Lens = new LensSize(LensWidth / 100, LensHeight / 100);
        }
    }

    private void ApplyDock()
    {
        if (_ready && DockEdgeIndex >= 0)
        {
            _model.Dock = new DockLayout((DockEdge)DockEdgeIndex, DockSize / 100);
        }
    }
}
