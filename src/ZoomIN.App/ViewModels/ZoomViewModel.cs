using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Views;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;

namespace ZoomIN.App.ViewModels;

/// <summary>Mode, levels per monitor, glide, lens, docked area and smoothing. Applies live.</summary>
public sealed partial class ZoomViewModel : ObservableObject
{
    private readonly PageContext _context;
    private readonly bool _ready;

    public ZoomViewModel(PageContext context)
    {
        _context = context;
        var model = context.Controller.Model;
        ModeIndex = (int)model.Mode;
        PerMonitor = model.PerMonitor;
        AnimationMs = Math.Round(model.AnimationSeconds * 1000);
        LensWidth = Math.Round(model.Lens.WidthFraction * 100);
        LensHeight = Math.Round(model.Lens.HeightFraction * 100);
        DockEdgeIndex = (int)model.Dock.Edge;
        DockSize = Math.Round(model.Dock.Fraction * 100);
        SmoothingIndex = (int)context.Controller.Effects.Current.Smoothing;
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
            _context.Controller.SelectMode((ZoomMode)value);
        }
    }

    partial void OnPerMonitorChanged(bool value)
    {
        if (_ready)
        {
            _context.Controller.Model.PerMonitor = value;
        }
    }

    partial void OnAnimationMsChanged(double value)
    {
        if (_ready)
        {
            _context.Controller.Model.AnimationSeconds = value / 1000;
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
            _context.Controller.Effects.SetSmoothing((Smoothing)value);
        }
    }

    private void ApplyLens()
    {
        if (_ready)
        {
            _context.Controller.Model.Lens = new LensSize(LensWidth / 100, LensHeight / 100);
        }
    }

    private void ApplyDock()
    {
        if (_ready && DockEdgeIndex >= 0)
        {
            _context.Controller.Model.Dock = new DockLayout((DockEdge)DockEdgeIndex, DockSize / 100);
        }
    }
}
