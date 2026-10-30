using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Zoom;

/// <summary>
/// Connects hotkeys, the mouse wheel and the UI to the <see cref="ZoomModel"/>, starts the zoom
/// output and persists the zoom levels (debounced, and once more on shutdown).
/// Steps apply to the monitor under the mouse.
/// </summary>
public sealed class ZoomController : IDisposable
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(1);

    private readonly IZoomOutput _output;
    private readonly IHotkeyService _hotkeys;
    private readonly IMouseWheelHook _wheel;
    private readonly IMonitorProvider _monitors;
    private readonly ICursorSource _cursor;
    private readonly ISettingsStore _store;
    private readonly ILog _log;
    private readonly Timer _saveTimer;
    private readonly AppSettings _settings;

    public ZoomController(
        ZoomModel model,
        AppSettings settings,
        IZoomOutput output,
        IHotkeyService hotkeys,
        IMouseWheelHook wheel,
        IMonitorProvider monitors,
        ICursorSource cursor,
        ISettingsStore store,
        ILog log)
    {
        Model = model;
        _settings = settings;
        _output = output;
        _hotkeys = hotkeys;
        _wheel = wheel;
        _monitors = monitors;
        _cursor = cursor;
        _store = store;
        _log = log;
        _saveTimer = new Timer(_ => Save());
    }

    public ZoomModel Model { get; }

    public ZoomStatus Status => _output.Status;

    /// <summary>Hotkeys that could not be registered because another application owns them.</summary>
    public IReadOnlyList<HotkeyGesture> UnavailableHotkeys { get; private set; } = [];

    public IReadOnlyList<string> MonitorIds() => _monitors.MonitorIds();

    public void Initialize()
    {
        var unavailable = new List<HotkeyGesture>();
        Register(_settings.ToggleHotkey, Toggle, repeat: false, unavailable);
        Register(_settings.ZoomInHotkey, () => Step(1), repeat: true, unavailable);
        Register(_settings.ZoomOutHotkey, () => Step(-1), repeat: true, unavailable);
        UnavailableHotkeys = unavailable;

        _wheel.Start(_settings.WheelModifiers, Step);
        Model.Changed += ScheduleSave;
        _output.Start();
    }

    public void Toggle() => Model.Toggle(_monitors.MonitorIds());

    public void Dispose()
    {
        Model.Changed -= ScheduleSave;
        _saveTimer.Dispose();
        Save();
    }

    private void Step(double notches)
    {
        var at = _cursor.TryGetPosition(out var position) ? position : default;
        Model.Step(notches, _monitors.MonitorAt(at), _monitors.MonitorIds());
    }

    private void Register(HotkeyGesture gesture, Action action, bool repeat, List<HotkeyGesture> unavailable)
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

    // Wheel zooming changes the level many times per second; write the file once it settles.
    private void ScheduleSave() => _saveTimer.Change(SaveDelay, Timeout.InfiniteTimeSpan);

    private void Save() => _store.Save(_settings with
    {
        LastZoomLevel = Model.SharedLastLevel,
        MonitorZoomLevels = Model.LastLevels(),
        PerMonitorZoom = Model.PerMonitor,
    });
}
