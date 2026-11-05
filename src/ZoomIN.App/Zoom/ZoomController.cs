using ZoomIN.App.Pointer;
using ZoomIN.App.Settings;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
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
    private readonly IWindowFilterSource _windowFilter;
    private readonly IHotkeyService _hotkeys;
    private readonly IMouseWheelHook _wheel;
    private readonly IInputActivitySource _activity;
    private readonly IMonitorProvider _monitors;
    private readonly ICursorSource _cursor;
    private readonly ILogger _log;
    private readonly DockedStripSync _dockedStrip;

    public ZoomController(
        SettingsService settings,
        ProfileService profiles,
        IZoomOutput output,
        ITrackingSource trackingSource,
        IWindowFilterSource windowFilter,
        IHotkeyService hotkeys,
        IMouseWheelHook wheel,
        IInputActivitySource activity,
        IMonitorProvider monitors,
        ICursorSource cursor,
        IScreenReservation reservation,
        ILogger<ZoomController> log)
    {
        _settings = settings;
        _profiles = profiles;
        _output = output;
        _trackingSource = trackingSource;
        _windowFilter = windowFilter;
        _hotkeys = hotkeys;
        _wheel = wheel;
        _activity = activity;
        _monitors = monitors;
        _cursor = cursor;
        _log = log;
        _dockedStrip = new DockedStripSync(Model, monitors, reservation);
    }

    public ZoomModel Model => _settings.Models.Zoom;

    public EffectsModel Effects => _settings.Models.Effects;

    public TrackingModel Tracking => _settings.Models.Tracking;

    public WindowFilterModel Windows => _settings.Models.Windows;

    public ZoomStatus Status => _output.Status;

    /// <summary>After the window filter shortcut: the new rule, or null if nothing changed. On the hotkey thread.</summary>
    public event Action<WindowFilterRule?>? WindowFilterToggled;

    /// <summary>Hotkeys that could not be registered because another application owns them.</summary>
    public IReadOnlyList<HotkeyGesture> UnavailableHotkeys { get; private set; } = [];

    public IReadOnlyList<string> MonitorIds() => _monitors.MonitorIds();

    public void Initialize()
    {
        RegisterHotkeys();
        _wheel.Start(_settings.Current.WheelModifiers, Step);
        _activity.Start(new PointerActivity(Effects));
        Model.Changed += _dockedStrip.Update;
        _output.Start();
        _trackingSource.Start(Tracking);
        _windowFilter.Start(Windows);
    }

    /// <summary>Registers all shortcuts again, e.g. after one was changed in the settings.</summary>
    public void ReloadHotkeys()
    {
        _hotkeys.UnregisterAll();
        RegisterHotkeys();
    }

    /// <summary>Releases all shortcuts while the hotkey editor records a new combination.</summary>
    public void SuspendHotkeys() => _hotkeys.UnregisterAll();

    public void Toggle() => Model.Toggle(MonitorUnderMouse(), _monitors.MonitorIds());

    public void Activate(ZoomMode mode) => Model.Activate(mode, MonitorUnderMouse(), _monitors.MonitorIds());

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

    /// <summary>Zoom in (+) or out (−) on the monitor under the mouse, e.g. from the overview page.</summary>
    public void ZoomBy(double steps) => Step(steps);

    /// <summary>Colour filter on/off for the monitor under the mouse (or all, if monitors share it).</summary>
    public void ToggleFilter() => Effects.ToggleFilter(MonitorUnderMouse());

    public string MonitorUnderMouse() =>
        _monitors.MonitorAt(_cursor.TryGetPosition(out var position) ? position : default);

    /// <summary>App + class of the windows open now, for the detail page of the filter per window.</summary>
    public IReadOnlyList<WindowKind> OpenWindowKinds() => _windowFilter.OpenKinds();

    /// <summary>
    /// Shortcut "filter for the active window on/off" (ADR-0020): remembered for app + window
    /// class. Returns the new rule, or null if there is no window the filter acts on (or it is off).
    /// </summary>
    public WindowFilterRule? ToggleWindowFilter()
    {
        var options = Windows.Options;
        if (!options.Enabled || !_windowFilter.TryGetForeground(out var window))
        {
            WindowFilterToggled?.Invoke(null);
            return null;
        }

        var center = new PhysicalPoint(window.Bounds.X + (window.Bounds.Width / 2), window.Bounds.Y + (window.Bounds.Height / 2));
        var monitorFiltered = Effects.CurrentFor(_monitors.MonitorAt(center)).Filter.Enabled;
        var rule = WindowFilterToggle.Next(window.Kind, window.Palette, monitorFiltered, options);
        using (Telemetry.Source.StartActivity("zoomin.windowfilter.toggle")?
            .SetTag("process.name", window.Kind.Process)
            .SetTag("zoomin.choice", rule.Choice.ToString()))
        {
            Windows.SetOptions(options.WithRule(rule));
        }

        WindowFilterToggled?.Invoke(rule);
        return rule;
    }

    public void Dispose() => Model.Changed -= _dockedStrip.Update;

    private void RegisterHotkeys()
    {
        var s = _settings.Current;
        var unavailable = new List<HotkeyGesture>();
        void Register(HotkeyGesture gesture, Action action, bool repeat = false)
        {
            // Unassigned (e.g. the window filter shortcut from the start).
            if (gesture.VirtualKey == 0)
            {
                return;
            }

            if (_hotkeys.Register(gesture, action, repeat))
            {
                _log.HotkeyRegistered(gesture);
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
        Register(s.FilterHotkey, ToggleFilter);
        Register(s.NextFilterHotkey, () => Effects.NextFilter(MonitorUnderMouse()));
        if (Effects.Current.Cursor.LocatorTriggers.HasFlag(LocatorTriggers.Hotkey))
        {
            Register(s.LocateHotkey, Effects.Locate);
        }

        Register(s.CrosshairHotkey, Effects.ToggleCrosshair);
        Register(s.TrackingHotkey, Tracking.ToggleEnabled);
        Register(s.ProfileHotkey, _profiles.Next);
        Register(s.WindowFilterHotkey, () => ToggleWindowFilter());
        UnavailableHotkeys = unavailable;
    }

    private void Step(double notches) => Model.Step(notches, MonitorUnderMouse(), _monitors.MonitorIds());
}
