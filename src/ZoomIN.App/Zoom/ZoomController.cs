using ZoomIN.App.Settings;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Zoom;

/// <summary>
/// Connects hotkeys, the mouse wheel, tray and UI to the models, starts zoom output and focus
/// tracking and keeps the docked strip reserved. Commands apply to the monitor under the mouse.
/// Saving is done by <see cref="SettingsService"/>.
/// </summary>
public sealed class ZoomController : IDisposable
{
    private readonly SettingsService _settings;
    private readonly ProfileService _profiles;
    private readonly IZoomOutput _output;
    private readonly ITrackingSource _trackingSource;
    private readonly IHotkeyService _hotkeys;
    private readonly IMouseWheelHook _wheel;
    private readonly IMonitorProvider _monitors;
    private readonly ICursorSource _cursor;
    private readonly ILog _log;
    private readonly DockedStripSync _dockedStrip;

    public ZoomController(
        SettingsService settings,
        ProfileService profiles,
        IZoomOutput output,
        ITrackingSource trackingSource,
        IHotkeyService hotkeys,
        IMouseWheelHook wheel,
        IMonitorProvider monitors,
        ICursorSource cursor,
        IScreenReservation reservation,
        ILog log)
    {
        _settings = settings;
        _profiles = profiles;
        _output = output;
        _trackingSource = trackingSource;
        _hotkeys = hotkeys;
        _wheel = wheel;
        _monitors = monitors;
        _cursor = cursor;
        _log = log;
        _dockedStrip = new DockedStripSync(Model, monitors, reservation);
    }

    public ZoomModel Model => _settings.Models.Zoom;

    public EffectsModel Effects => _settings.Models.Effects;

    public TrackingModel Tracking => _settings.Models.Tracking;

    public ZoomStatus Status => _output.Status;

    /// <summary>Hotkeys that could not be registered because another application owns them.</summary>
    public IReadOnlyList<HotkeyGesture> UnavailableHotkeys { get; private set; } = [];

    public IReadOnlyList<string> MonitorIds() => _monitors.MonitorIds();

    public void Initialize()
    {
        RegisterHotkeys();
        _wheel.Start(_settings.Current.WheelModifiers, Step);
        Model.Changed += _dockedStrip.Update;
        _output.Start();
        _trackingSource.Start(Tracking);
    }

    /// <summary>Registers all shortcuts again, e.g. after one was changed in the settings.</summary>
    public void ReloadHotkeys()
    {
        _hotkeys.UnregisterAll();
        RegisterHotkeys();
    }

    /// <summary>Releases all shortcuts while the hotkey editor records a new combination.</summary>
    public void SuspendHotkeys() => _hotkeys.UnregisterAll();

    public void Toggle() => Model.Toggle(MonitorAtMouse(), _monitors.MonitorIds());

    public void Activate(ZoomMode mode) => Model.Activate(mode, MonitorAtMouse(), _monitors.MonitorIds());

    /// <summary>Mode chosen in the settings: switches over if zooming, otherwise only preselects it.</summary>
    public void SelectMode(ZoomMode mode)
    {
        if (mode == Model.Mode)
        {
            return;
        }

        if (Model.IsOn)
        {
            Activate(mode);
        }
        else
        {
            Model.SetMode(mode);
        }
    }

    public void Dispose() => Model.Changed -= _dockedStrip.Update;

    private void RegisterHotkeys()
    {
        var s = _settings.Current;
        var unavailable = new List<HotkeyGesture>();
        void Register(HotkeyGesture gesture, Action action, bool repeat = false)
        {
            if (_hotkeys.Register(gesture, action, repeat))
            {
                _log.Info($"Hotkey {gesture} registered");
            }
            else
            {
                unavailable.Add(gesture);
            }
        }

        Register(s.ToggleHotkey, Toggle);
        Register(s.ZoomInHotkey, () => Step(1), repeat: true);
        Register(s.ZoomOutHotkey, () => Step(-1), repeat: true);
        Register(s.FullscreenHotkey, () => Activate(ZoomMode.Fullscreen));
        Register(s.LensHotkey, () => Activate(ZoomMode.Lens));
        Register(s.DockedHotkey, () => Activate(ZoomMode.Docked));
        Register(HotkeyGesture.LensWider, () => Model.ResizeLens(1, 0), repeat: true);
        Register(HotkeyGesture.LensNarrower, () => Model.ResizeLens(-1, 0), repeat: true);
        Register(HotkeyGesture.LensTaller, () => Model.ResizeLens(0, 1), repeat: true);
        Register(HotkeyGesture.LensShorter, () => Model.ResizeLens(0, -1), repeat: true);
        Register(s.FilterHotkey, Effects.ToggleFilter);
        Register(s.NextFilterHotkey, Effects.NextFilter);
        Register(s.LocateHotkey, Effects.Locate);
        Register(s.TrackingHotkey, Tracking.ToggleEnabled);
        Register(s.ProfileHotkey, _profiles.Next);
        UnavailableHotkeys = unavailable;
    }

    private void Step(double notches) => Model.Step(notches, MonitorAtMouse(), _monitors.MonitorIds());

    private string MonitorAtMouse() =>
        _monitors.MonitorAt(_cursor.TryGetPosition(out var position) ? position : default);
}
