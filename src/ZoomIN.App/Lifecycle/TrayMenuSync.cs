using ZoomIN.App.Freeze;
using ZoomIN.App.Localization;
using ZoomIN.App.Settings;
using ZoomIN.App.Zoom;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Core.Localization;
using ZoomIN.Core.Modes;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.App.Lifecycle;

/// <summary>
/// Keeps the tray menu and tooltip in step with zoom, profile and language (M7), and carries out
/// the menu's zoom and profile commands. Opening the window and exiting stay with <see cref="AppShell"/>.
/// </summary>
public sealed class TrayMenuSync
{
    private readonly ITrayIcon _tray;
    private readonly SettingsService _settings;
    private readonly ProfileService _profiles;
    private readonly ZoomCommands _zoom;
    private readonly WindowFilterCommands _windowFilter;
    private readonly MonitorFreezeController _freeze;
    private readonly MonitorSwitchController _switch;
    private readonly ILocalizer _texts;

    public TrayMenuSync(
        ITrayIcon tray, SettingsService settings, ProfileService profiles, ZoomCommands zoom,
        WindowFilterCommands windowFilter, MonitorFreezeController freeze, MonitorSwitchController monitorSwitch, ILocalizer texts)
    {
        _tray = tray;
        _settings = settings;
        _profiles = profiles;
        _zoom = zoom;
        _windowFilter = windowFilter;
        _freeze = freeze;
        _switch = monitorSwitch;
        _texts = texts;
    }

    public void Start()
    {
        _settings.Models.Zoom.Changed += Update;
        _profiles.Changed += Update;
        _texts.LanguageChanged += Update;
        Update();

        // Short status after the window filter shortcut (ADR-0020); the tray works from any thread.
        _windowFilter.Toggled += rule => _tray.Notify(Text.Get("WinFilter.Title"), WindowFilterStatus(rule));

        // Why Ctrl+Alt+H or Ctrl+Alt+N did nothing, e.g. "freezing needs at least two monitors" (G2).
        _freeze.Refused += hint => _tray.Notify(_texts.Get("Freeze.Title"), hint);
        _switch.Refused += hint => _tray.Notify(_texts.Get("Freeze.Title"), hint);
    }

    /// <summary>Runs a zoom or profile command; the shell handles Open and Exit itself.</summary>
    public void Handle(TrayCommand command)
    {
        switch (command)
        {
            case TrayCommand.ToggleZoom: _zoom.Toggle(); break;
            case TrayCommand.Fullscreen: _zoom.Activate(ZoomMode.Fullscreen); break;
            case TrayCommand.Lens: _zoom.Activate(ZoomMode.Lens); break;
            case TrayCommand.Docked: _zoom.Activate(ZoomMode.Docked); break;
            case TrayCommand.NextProfile: _profiles.Next(); break;
            default: break;
        }
    }

    private static string WindowFilterStatus(WindowFilterRule? rule) => rule switch
    {
        null => Text.Get("WinFilter.Status.Nothing"),
        { Choice: WindowFilterChoice.Filter } => Text.Format("WinFilter.Status.On", rule.Process, Text.Get("Filter." + rule.Filter)),
        _ => Text.Format("WinFilter.Status.Off", rule.Process),
    };

    private void Update()
    {
        var model = _settings.Models.Zoom;
        var mode = model.Mode;
        var on = model.IsOn;
        _tray.Show(new TrayMenu(
            Text.Format("TrayTooltip", Text.ModeName(mode), on ? Text.On : Text.Off),
            [
                new(TrayCommand.Open, Text.TraySettings),
                new(TrayCommand.Open, null),
                new(TrayCommand.ToggleZoom, Text.TrayToggle, on),
                new(TrayCommand.Fullscreen, Text.ModeFullscreen, mode == ZoomMode.Fullscreen),
                new(TrayCommand.Lens, Text.ModeLens, mode == ZoomMode.Lens),
                new(TrayCommand.Docked, Text.ModeDocked, mode == ZoomMode.Docked),
                new(TrayCommand.Open, null),
                new(TrayCommand.NextProfile, Text.Format("TrayNextProfile", _profiles.Active)),
                new(TrayCommand.Open, null),
                new(TrayCommand.Exit, Text.TrayExit),
            ]));
    }
}
