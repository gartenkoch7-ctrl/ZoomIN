using Microsoft.UI.Dispatching;
using ZoomIN.App.Localization;
using ZoomIN.App.Settings;
using ZoomIN.App.Views;
using ZoomIN.App.Zoom;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Core.Modes;

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
    private readonly IUpdateChecker _updates;
    private readonly ILog _log;
    private readonly PageContext _pages;
    private readonly DispatcherQueue _dispatcher;
    private SettingsWindow? _window;

    public AppShell(
        ZoomController controller, SettingsService settings, ProfileService profiles, ITrayIcon tray,
        IUpdateChecker updates, IAutostart autostart, ILog log)
    {
        _controller = controller;
        _settings = settings;
        _profiles = profiles;
        _tray = tray;
        _updates = updates;
        _log = log;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _pages = new PageContext(settings, profiles, controller, autostart, () => _window?.Handle ?? 0, () => _window?.ShowGeneral());
    }

    /// <summary>Raised on the UI thread when the user chose "Exit".</summary>
    public event Action? ExitRequested;

    public void Start()
    {
        Text.SetLanguage(_settings.Current.Language);
        _controller.Initialize();

        _tray.Command += command => _dispatcher.TryEnqueue(() => OnTrayCommand(command));
        _controller.Model.Changed += UpdateTray;
        _profiles.Changed += UpdateTray;
        Text.LanguageChanged += UpdateTray;
        UpdateTray();

        if (!_settings.Current.FirstRunCompleted)
        {
            OpenSettings();
        }

        _ = CheckForUpdatesAsync();
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

    private async Task CheckForUpdatesAsync()
    {
        var settings = _settings.Current;
        if (!settings.CheckForUpdates || string.IsNullOrWhiteSpace(settings.UpdateFeedUrl))
        {
            return;
        }

        // Not during start-up: the check is never urgent.
        await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        var current = typeof(AppShell).Assembly.GetName().Version ?? new Version(0, 0);
        if (await _updates.NewerVersionAsync(settings.UpdateFeedUrl, current, CancellationToken.None).ConfigureAwait(false) is { } newer)
        {
            _log.Info($"Update available: {newer}");
            _tray.Notify(Text.UpdateTitle, Text.Format("UpdateText", newer));
        }
    }

    internal static string ModeName(ZoomMode mode) => mode switch
    {
        ZoomMode.Lens => Text.ModeLens,
        ZoomMode.Docked => Text.ModeDocked,
        _ => Text.ModeFullscreen,
    };
}
