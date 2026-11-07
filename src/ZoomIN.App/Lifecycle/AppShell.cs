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
public sealed class AppShell : IShellActions
{
    private readonly ZoomController _controller;
    private readonly ZoomCommands _zoom;
    private readonly WindowFilterCommands _windowFilter;
    private readonly SettingsService _settings;
    private readonly ProfileService _profiles;
    private readonly ITrayIcon _tray;
    private readonly UpdateService _updates;
    private readonly IUiAccessProbe _uiAccess;
    private readonly ILogger _log;
    private readonly ILocalizer _texts;
    private readonly PageServices _pages;
    private readonly DispatcherQueue _dispatcher;
    private SettingsWindow? _window;

    public AppShell(
        ZoomController controller, ZoomCommands zoom, WindowFilterCommands windowFilter, SettingsService settings,
        ProfileService profiles, ITrayIcon tray, UpdateService updates, IUiAccessProbe uiAccess, PageServices pages,
        ILocalizer texts, ILogger<AppShell> log)
    {
        _zoom = zoom;
        _windowFilter = windowFilter;
        _pages = pages;
        _texts = texts;
        _controller = controller;
        _settings = settings;
        _profiles = profiles;
        _tray = tray;
        _updates = updates;
        _uiAccess = uiAccess;
        _log = log;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
    }

    /// <summary>Raised on the UI thread when the user chose "Exit".</summary>
    public event Action? ExitRequested;

    public void Start()
    {
        _texts.SetLanguage(_settings.Current.Language);
        _controller.Initialize();

        _tray.Command += command => _dispatcher.TryEnqueue(() => OnTrayCommand(command));
        _settings.Models.Zoom.Changed += UpdateTray;
        _profiles.Changed += UpdateTray;
        _texts.LanguageChanged += UpdateTray;
        UpdateTray();

        // Short status after the window filter shortcut (ADR-0020); the tray works from any thread.
        _windowFilter.Toggled += rule => _tray.Notify(Text.Get("WinFilter.Title"), WindowFilterStatus(rule));

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
        var window = new SettingsWindow(_pages, _settings.Current.FirstRunCompleted);
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
            case TrayCommand.ToggleZoom: _zoom.Toggle(); break;
            case TrayCommand.Fullscreen: _zoom.Activate(ZoomMode.Fullscreen); break;
            case TrayCommand.Lens: _zoom.Activate(ZoomMode.Lens); break;
            case TrayCommand.Docked: _zoom.Activate(ZoomMode.Docked); break;
            case TrayCommand.NextProfile: _profiles.Next(); break;
            case TrayCommand.Exit:
                _window?.CloseForExit();
                ExitRequested?.Invoke();
                break;
        }
    }

    private void UpdateTray()
    {
        var model = _settings.Models.Zoom;
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

    /// <inheritdoc/>
    public nint SettingsWindowHandle => _window?.Handle ?? 0;

    /// <inheritdoc/>
    public void ShowOverview() => _window?.ShowOverview();

    /// <inheritdoc/>
    public void ExitForUpdate() => _dispatcher.TryEnqueue(() =>
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
