using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Localization;
using ZoomIN.App.Settings;
using ZoomIN.App.Zoom;
using ZoomIN.Core.Modes;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.App.ViewModels;

/// <summary>
/// Start page: zoom on/off, mode, level, quick switches, profile and status. The state can also
/// change through hotkeys and the tray, so the page calls <see cref="Refresh"/> twice a second.
/// </summary>
public sealed partial class OverviewViewModel : ObservableObject
{
    private readonly RenderModels _models;
    private readonly ZoomCommands _zoom;
    private readonly ZoomController _engine;
    private readonly ProfileService _profiles;
    private bool _syncing = true;

    public OverviewViewModel(SettingsService settings, ZoomCommands zoom, ZoomController engine, ProfileService profiles)
    {
        _models = settings.Models;
        _zoom = zoom;
        _engine = engine;
        _profiles = profiles;
        Refresh();
        _syncing = false;
    }

    public IReadOnlyList<string> ProfileNames => _profiles.Names();

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    [ObservableProperty]
    public partial bool? Fullscreen { get; set; }

    [ObservableProperty]
    public partial bool? Lens { get; set; }

    [ObservableProperty]
    public partial bool? Docked { get; set; }

    [ObservableProperty]
    public partial string LevelText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool FilterOn { get; set; }

    [ObservableProperty]
    public partial bool CrosshairOn { get; set; }

    [ObservableProperty]
    public partial bool TrackingOn { get; set; }

    [ObservableProperty]
    public partial string? Profile { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Severity))]
    public partial bool StatusOk { get; set; }

    public Microsoft.UI.Xaml.Controls.InfoBarSeverity Severity =>
        StatusOk ? Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success : Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error;

    public void ZoomIn() => _zoom.ZoomBy(1);

    public void ZoomOut() => _zoom.ZoomBy(-1);

    public void Locate() => _models.Effects.Locate();

    /// <summary>Reads the current state from the models without sending it back.</summary>
    public void Refresh()
    {
        var was = _syncing;
        _syncing = true;
        var model = _models.Zoom;
        var effects = _models.Effects.CurrentFor(_zoom.MonitorUnderMouse());
        IsOn = model.IsOn;
        Fullscreen = model.Mode == ZoomMode.Fullscreen;
        Lens = model.Mode == ZoomMode.Lens;
        Docked = model.Mode == ZoomMode.Docked;
        var level = _zoom.MonitorIds().Select(id => model.IsOn ? model.TargetFor(id) : model.LastLevelFor(model.Mode, id))
            .DefaultIfEmpty(1).Max();
        LevelText = string.Create(CultureInfo.CurrentCulture, $"{level:0.0#} ×");
        FilterOn = effects.Filter.Enabled;
        CrosshairOn = effects.Cursor.CrosshairEnabled;
        TrackingOn = _models.Tracking.Options.Enabled;
        Profile = _profiles.Active;

        var status = _engine.Status;
        StatusOk = status.LastError is null;
        StatusText = status.LastError is { } error
            ? Text.Format("Overview.StatusError", error)
            : status.IsVisible
                ? Text.Format("Overview.StatusZooming", status.FramesPerSecond)
                : Text.Get("Overview.StatusReady");
        _syncing = was;
    }

    partial void OnIsOnChanged(bool value)
    {
        if (!_syncing && value != _models.Zoom.IsOn)
        {
            _zoom.Toggle();
        }
    }

    partial void OnFullscreenChanged(bool? value) => Select(value, ZoomMode.Fullscreen);

    partial void OnLensChanged(bool? value) => Select(value, ZoomMode.Lens);

    partial void OnDockedChanged(bool? value) => Select(value, ZoomMode.Docked);

    partial void OnFilterOnChanged(bool value)
    {
        if (!_syncing && value != _models.Effects.CurrentFor(_zoom.MonitorUnderMouse()).Filter.Enabled)
        {
            _zoom.ToggleFilter();
        }
    }

    partial void OnCrosshairOnChanged(bool value)
    {
        if (!_syncing && value != _models.Effects.Current.Cursor.CrosshairEnabled)
        {
            _models.Effects.ToggleCrosshair();
        }
    }

    partial void OnTrackingOnChanged(bool value)
    {
        if (!_syncing && value != _models.Tracking.Options.Enabled)
        {
            _models.Tracking.ToggleEnabled();
        }
    }

    partial void OnProfileChanged(string? value)
    {
        if (!_syncing && value is not null && value != _profiles.Active)
        {
            _profiles.SwitchTo(value);
        }
    }

    private void Select(bool? selected, ZoomMode mode)
    {
        if (!_syncing && selected == true)
        {
            _zoom.SelectMode(mode);
        }
    }
}
