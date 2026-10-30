using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Zoom;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.App.Zoom;

/// <summary>
/// Connects hotkeys, the mouse wheel and the UI to the models, starts zoom output and focus
/// tracking, keeps the docked strip reserved and persists the settings (debounced, and on
/// shutdown). Commands apply to the monitor under the mouse.
/// </summary>
public sealed class ZoomController : IDisposable
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(1);

    private readonly AppSettings _settings;
    private readonly IZoomOutput _output;
    private readonly ITrackingSource _trackingSource;
    private readonly IHotkeyService _hotkeys;
    private readonly IMouseWheelHook _wheel;
    private readonly IMonitorProvider _monitors;
    private readonly ICursorSource _cursor;
    private readonly ISettingsStore _store;
    private readonly ILog _log;
    private readonly DockedStripSync _dockedStrip;
    private readonly Timer _saveTimer;

    public ZoomController(
        RenderModels models,
        AppSettings settings,
        IZoomOutput output,
        ITrackingSource trackingSource,
        IHotkeyService hotkeys,
        IMouseWheelHook wheel,
        IMonitorProvider monitors,
        ICursorSource cursor,
        IScreenReservation reservation,
        ISettingsStore store,
        ILog log)
    {
        Model = models.Zoom;
        Effects = models.Effects;
        Tracking = models.Tracking;
        _settings = settings;
        _output = output;
        _trackingSource = trackingSource;
        _hotkeys = hotkeys;
        _wheel = wheel;
        _monitors = monitors;
        _cursor = cursor;
        _store = store;
        _log = log;
        _dockedStrip = new DockedStripSync(Model, monitors, reservation);
        _saveTimer = new Timer(_ => Save());
    }

    public ZoomModel Model { get; }

    public EffectsModel Effects { get; }

    public TrackingModel Tracking { get; }

    public ZoomStatus Status => _output.Status;

    /// <summary>Hotkeys that could not be registered because another application owns them.</summary>
    public IReadOnlyList<HotkeyGesture> UnavailableHotkeys { get; private set; } = [];

    public IReadOnlyList<string> MonitorIds() => _monitors.MonitorIds();

    public void Initialize()
    {
        RegisterHotkeys();
        _wheel.Start(_settings.WheelModifiers, Step);
        Model.Changed += OnModelChanged;
        Effects.Changed += ScheduleSave;
        Tracking.Changed += ScheduleSave;
        _output.Start();
        _trackingSource.Start(Tracking);
    }

    public void Toggle() => Model.Toggle(MonitorAtMouse(), _monitors.MonitorIds());

    public void Activate(ZoomMode mode) => Model.Activate(mode, MonitorAtMouse(), _monitors.MonitorIds());

    public void Dispose()
    {
        Model.Changed -= OnModelChanged;
        Effects.Changed -= ScheduleSave;
        Tracking.Changed -= ScheduleSave;
        _saveTimer.Dispose();
        Save();
    }

    private void RegisterHotkeys()
    {
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

        Register(_settings.ToggleHotkey, Toggle);
        Register(_settings.ZoomInHotkey, () => Step(1), repeat: true);
        Register(_settings.ZoomOutHotkey, () => Step(-1), repeat: true);
        Register(_settings.FullscreenHotkey, () => Activate(ZoomMode.Fullscreen));
        Register(_settings.LensHotkey, () => Activate(ZoomMode.Lens));
        Register(_settings.DockedHotkey, () => Activate(ZoomMode.Docked));
        Register(HotkeyGesture.LensWider, () => Model.ResizeLens(1, 0), repeat: true);
        Register(HotkeyGesture.LensNarrower, () => Model.ResizeLens(-1, 0), repeat: true);
        Register(HotkeyGesture.LensTaller, () => Model.ResizeLens(0, 1), repeat: true);
        Register(HotkeyGesture.LensShorter, () => Model.ResizeLens(0, -1), repeat: true);
        Register(_settings.FilterHotkey, Effects.ToggleFilter);
        Register(_settings.NextFilterHotkey, Effects.NextFilter);
        Register(_settings.LocateHotkey, Effects.Locate);
        Register(_settings.TrackingHotkey, Tracking.ToggleEnabled);
        UnavailableHotkeys = unavailable;
    }

    private void Step(double notches) => Model.Step(notches, MonitorAtMouse(), _monitors.MonitorIds());

    private string MonitorAtMouse() =>
        _monitors.MonitorAt(_cursor.TryGetPosition(out var position) ? position : default);

    private void OnModelChanged()
    {
        _dockedStrip.Update();
        ScheduleSave();
    }

    // Wheel zooming changes the level many times per second; write the file once it settles.
    private void ScheduleSave() => _saveTimer.Change(SaveDelay, Timeout.InfiniteTimeSpan);

    private void Save()
    {
        var settings = ZoomModelFactory.Apply(_settings, Model);
        settings = EffectsFactory.Apply(settings, Effects);
        _store.Save(TrackingFactory.Apply(settings, Tracking));
    }
}
