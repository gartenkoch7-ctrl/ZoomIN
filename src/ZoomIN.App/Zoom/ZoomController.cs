using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Zoom;

/// <summary>
/// Connects hotkeys, the mouse wheel and the UI to the <see cref="ZoomModel"/>, starts the zoom
/// output and persists the last zoom level (debounced, and once more on shutdown).
/// </summary>
public sealed class ZoomController : IDisposable
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(1);

    private readonly IZoomOutput _output;
    private readonly IHotkeyService _hotkeys;
    private readonly IMouseWheelHook _wheel;
    private readonly ISettingsStore _store;
    private readonly ILog _log;
    private readonly Timer _saveTimer;
    private AppSettings _settings;

    public ZoomController(
        ZoomModel model,
        AppSettings settings,
        IZoomOutput output,
        IHotkeyService hotkeys,
        IMouseWheelHook wheel,
        ISettingsStore store,
        ILog log)
    {
        Model = model;
        _settings = settings;
        _output = output;
        _hotkeys = hotkeys;
        _wheel = wheel;
        _store = store;
        _log = log;
        _saveTimer = new Timer(_ => Save());
    }

    public ZoomModel Model { get; }

    public ZoomStatus Status => _output.Status;

    /// <summary>Hotkeys that could not be registered because another application owns them.</summary>
    public IReadOnlyList<HotkeyGesture> UnavailableHotkeys { get; private set; } = [];

    public void Initialize()
    {
        var unavailable = new List<HotkeyGesture>();
        Register(_settings.ToggleHotkey, Model.Toggle, repeat: false, unavailable);
        Register(_settings.ZoomInHotkey, () => Model.Step(1), repeat: true, unavailable);
        Register(_settings.ZoomOutHotkey, () => Model.Step(-1), repeat: true, unavailable);
        UnavailableHotkeys = unavailable;

        _wheel.Start(_settings.WheelModifiers, Model.Step);
        Model.Changed += ScheduleSave;
        _output.Start();
    }

    public void Toggle() => Model.Toggle();

    public void Dispose()
    {
        Model.Changed -= ScheduleSave;
        _saveTimer.Dispose();
        Save();
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

    private void Save()
    {
        var updated = _settings with { LastZoomLevel = Model.LastLevel };
        if (updated != _settings)
        {
            _settings = updated;
            _store.Save(updated);
        }
    }
}
