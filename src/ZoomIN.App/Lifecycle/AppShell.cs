using Microsoft.UI.Dispatching;
using ZoomIN.App.Localization;
using ZoomIN.App.SecureDesktop;
using ZoomIN.App.Settings;
using ZoomIN.App.Updates;
using ZoomIN.App.Views;
using ZoomIN.App.Zoom;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Folders;
using ZoomIN.Core.Localization;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.App.Lifecycle;

/// <summary>
/// Runs ZoomIN as a tray application (M7): starts zoom and tray, opens the settings window on
/// demand, shows the first-run assistant once, checks for updates if allowed, and exits cleanly.
/// </summary>
public sealed class AppShell
{
    private readonly ZoomController _controller;
    private readonly SettingsService _settings;
    private readonly ProfileService _profiles;
    private readonly ITrayIcon _tray;
    private readonly UpdateService _updates;
    private readonly IUiAccessProbe _uiAccess;
    private readonly ILogger _log;
    private readonly ILocalizer _texts;
    private readonly PageContext _pages;
    private readonly DispatcherQueue _dispatcher;
    private SettingsWindow? _window;

    public AppShell(
        ZoomController controller, SettingsService settings, ProfileService profiles, ITrayIcon tray,
        UpdateService updates, IAutostart autostart, ICursorShapeSource cursorShapes, IUiAccessProbe uiAccess,
        SetupLauncher setup, IAppFolders folders, IDiagnosticsPackage diagnostics,
        ISecureDesktopSwitch secureDesktopSwitch, ILocalizer texts, ILogger<AppShell> log)
    {
        _texts = texts;
        _controller = controller;
        _settings = settings;
        _profiles = profiles;
        _tray = tray;
        _updates = updates;
        _uiAccess = uiAccess;
        _log = log;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _pages = new PageContext(
            settings, profiles, controller, autostart, cursorShapes, updates, uiAccess, setup, folders, diagnostics, secureDesktopSwitch,
            () => _window?.Handle ?? 0, () => _window?.ShowOverview(), ExitForUpdate);
    }

    /// <summary>Raised on the UI thread when the user chose "Exit".</summary>
    public event Action? ExitRequested;

    public void Start()
    {
        _texts.SetLanguage(_settings.Current.Language);
        _controller.Initialize();

        _tray.Command += command => _dispatcher.TryEnqueue(() => OnTrayCommand(command));
        _controller.Model.Changed += UpdateTray;
        _profiles.Changed += UpdateTray;
        _texts.LanguageChanged += UpdateTray;
        UpdateTray();

        // Short status after the window filter shortcut (ADR-0020); the tray works from any thread.
        _controller.WindowFilterToggled += rule => _tray.Notify(Text.Get("WinFilter.Title"), WindowFilterStatus(rule));

        if (!_settings.Current.FirstRunCompleted)
        {
            OpenSettings();
        }

        _updates.UpdateAvailable += update => _tray.Notify(Text.UpdateTitle, Text.Format("UpdateText", update.Version));
        _ = RunUpdateChecksAsync();

        // Once per start; the overview keeps a banner with the install button (ADR-0017).
        if (!_uiAccess.IsActive)
        {
            _log.UiAccessInactive();
            _tray.Notify(Text.Get("UiAccess.Title"), Text.Get("UiAccess.Tray"));
        }
    }

    /// <summary>Thread-safe: e.g. called when a second ZoomIN start asks for the settings.</summary>
    public void OpenSettings() => _dispatcher.TryEnqueue(() =>
    {
        _window ??= CreateWindow();
        _window.ShowAndActivate();
    });

    private SettingsWindow CreateWindow()
    {
        var window = new SettingsWindow(_pages);
        window.HiddenToTray += OnHiddenToTray;
        return window;
    }

    private static string WindowFilterStatus(WindowFilterRule? rule) => rule switch
    {
        null => Text.Get("WinFilter.Status.Nothing"),
        { Choice: WindowFilterChoice.Filter } => Text.Format("WinFilter.Status.On", rule.Process, Text.Get("Filter." + rule.Filter)),
        _ => Text.Format("WinFilter.Status.Off", rule.Process),
    };

    private void OnHiddenToTray()
    {
        if (_settings.Current.CloseToTrayHintShown)
        {
            return;
        }

        _tray.Notify(Text.CloseToTrayTitle, Text.CloseToTrayText);
        _settings.Update(s => s with { CloseToTrayHintShown = true });
    }

    private void OnTrayCommand(TrayCommand command)
    {
        switch (command)
        {
            case TrayCommand.Open: OpenSettings(); break;
            case TrayCommand.ToggleZoom: _controller.Toggle(); break;
            case TrayCommand.Fullscreen: _controller.Activate(ZoomMode.Fullscreen); break;
            case TrayCommand.Lens: _controller.Activate(ZoomMode.Lens); break;
            case TrayCommand.Docked: _controller.Activate(ZoomMode.Docked); break;
            case TrayCommand.NextProfile: _profiles.Next(); break;
            case TrayCommand.Exit:
                _window?.CloseForExit();
                ExitRequested?.Invoke();
                break;
        }
    }

    private void UpdateTray()
    {
        var model = _controller.Model;
        var mode = model.Mode;
        var on = model.IsOn;
        _tray.Show(new TrayMenu(
            Text.Format("TrayTooltip", ModeName(mode), on ? Text.On : Text.Off),
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

    private async Task RunUpdateChecksAsync()
    {
        try
        {
            await _updates.RunChecksAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            // Never important enough to disturb the magnifier; the feed already logged it.
            _log.UpdateChecksStopped(ex.Message);
        }
    }

    /// <summary>After an update: start the new version (it waits for this process), then exit.</summary>
    private void ExitForUpdate() => _dispatcher.TryEnqueue(() =>
    {
        _updates.StartNewVersion();
        _window?.CloseForExit();
        ExitRequested?.Invoke();
    });

    internal static string ModeName(ZoomMode mode) => mode switch
    {
        ZoomMode.Lens => Text.ModeLens,
        ZoomMode.Docked => Text.ModeDocked,
        _ => Text.ModeFullscreen,
    };
}
