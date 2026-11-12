using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Zoom;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Localization;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.ViewModels;

/// <summary>Mode, levels per monitor, glide, lens, docked area, smoothing, sharpness and bold. Applies live.</summary>
public sealed partial class ZoomViewModel : ObservableObject
{
    private readonly ZoomModel _model;
    private readonly EffectsModel _effects;
    private readonly ZoomCommands _zoom;
    private readonly ILocalizer _texts;
    private readonly bool _ready;

    public ZoomViewModel(ZoomModel model, EffectsModel effects, ZoomCommands zoom, ILocalizer texts)
    {
        _texts = texts;
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
        Sharpness = Math.Round(effects.Current.Enhancement.Sharpness * 100);
        Bold = Math.Round(effects.Current.Enhancement.BoldPixels, 1);
        _ready = true;
    }

    /// <summary>"Schärfe" in %, 0–100 (M18, D1).</summary>
    [ObservableProperty]
    public partial double Sharpness { get; set; }

    /// <summary>"Fett" in px, 0–3 (M18, D1).</summary>
    [ObservableProperty]
    public partial double Bold { get; set; }

    /// <summary>The sliders act only with text-optimised smoothing; otherwise they are disabled with a hint.</summary>
    public bool EnhanceAvailable => TextEnhancement.WorksWith(_effects.Current.Smoothing);

    public string SharpnessDescription => EnhanceAvailable ? _texts.Get("ZoomSharpness.Desc") : _texts.Get("ZoomEnhance.NeedsText");

    public string BoldDescription => EnhanceAvailable ? _texts.Get("ZoomBold.Desc") : _texts.Get("ZoomEnhance.NeedsText");

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
    [NotifyPropertyChangedFor(nameof(EnhanceAvailable), nameof(SharpnessDescription), nameof(BoldDescription))]
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

    // Only the moved slider is taken over: the other value stays as the model has it (a profile
    // switch while the page is open must not get the page's old value back).
    partial void OnSharpnessChanged(double value) => ApplyEnhancement(e => e with { Sharpness = value / 100 });

    partial void OnBoldChanged(double value) => ApplyEnhancement(e => e with { BoldPixels = value });

    private void ApplyEnhancement(Func<TextEnhancement, TextEnhancement> change)
    {
        if (_ready)
        {
            _effects.SetEnhancement(change(_effects.Current.Enhancement));
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
