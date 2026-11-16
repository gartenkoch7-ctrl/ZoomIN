using Microsoft.UI.Dispatching;
using ZoomIN.App.Localization;
using ZoomIN.App.Pointer;
using ZoomIN.App.Settings;
using ZoomIN.App.Updates;
using ZoomIN.App.Views;
using ZoomIN.App.Views.Pages;
using ZoomIN.App.Zoom;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Core.Localization;

namespace ZoomIN.App.Lifecycle;

/// <summary>
/// Runs ZoomIN as a tray application (M7): starts zoom and tray, opens the settings window on
/// demand, shows the first-run assistant once and exits cleanly. The tray menu is
/// <see cref="TrayMenuSync"/>, the start-up notices are <see cref="StartupNotices"/>.
/// </summary>
public sealed class AppShell : IShellActions
{
    private readonly ZoomController _controller;
    private readonly SettingsService _settings;
    private readonly ITrayIcon _tray;
    private readonly TrayMenuSync _trayMenu;
    private readonly StartupNotices _notices;
    private readonly UpdateService _updates;
    private readonly ILocalizer _texts;
    private readonly PageServices _pages;
    private readonly DispatcherQueue _dispatcher;
    private readonly PointerConstraintController _pointerConstraint;
    private SettingsWindow? _window;

    public AppShell(
        ZoomController controller, SettingsService settings, ITrayIcon tray, TrayMenuSync trayMenu,
        StartupNotices notices, UpdateService updates, PageServices pages, ILocalizer texts,
        PointerConstraintController pointerConstraint)
    {
        _controller = controller;
        _pointerConstraint = pointerConstraint;
        _settings = settings;
        _tray = tray;
        _trayMenu = trayMenu;
        _notices = notices;
        _updates = updates;
        _pages = pages;
        _texts = texts;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
    }

    /// <summary>Raised on the UI thread when the user chose "Exit".</summary>
    public event Action? ExitRequested;

    public void Start()
    {
        _texts.SetLanguage(_settings.Current.Language);

        // Children of zoomin.app.start.shell (M14): the start runs synchronously on this thread.
        using (Telemetry.Source.StartActivity("zoomin.app.start.shell.engine"))
        {
            _controller.Initialize();

            // Mouse buffer and axis lock (A6): tray app only, the secure desktop keeps the plain pointer.
            _pointerConstraint.Start();
        }

        using (Telemetry.Source.StartActivity("zoomin.app.start.shell.tray"))
        {
            _tray.Command += command => _dispatcher.TryEnqueue(() => OnTrayCommand(command));
            _trayMenu.Start();

            if (!_settings.Current.FirstRunCompleted)
            {
                OpenSettings();
            }

            _notices.Start();
        }
    }

    /// <summary>Thread-safe: e.g. called when a second ZoomIN start asks for the settings.</summary>
    public void OpenSettings() => OpenSettings(null);

    /// <param name="page">Page to show, e.g. from a notice; not while the first-run assistant runs.</param>
    private void OpenSettings(Type? page) => _dispatcher.TryEnqueue(() =>
    {
        if (_window is null)
        {
            _window = CreateWindow(page);
        }
        else if (page is not null && _settings.Current.FirstRunCompleted)
        {
            _window.ShowPage(page);
        }

        _window.ShowAndActivate();
    });

    /// <inheritdoc/>
    public nint SettingsWindowHandle => _window?.Handle ?? 0;

    /// <inheritdoc/>
    public void ShowOverview() => _window?.ShowOverview();

    /// <inheritdoc/>
    public void ExitForUpdate() => _dispatcher.TryEnqueue(() =>
    {
        _updates.StartNewVersion();
        Exit();
    });

    private SettingsWindow CreateWindow(Type? page)
    {
        var window = new SettingsWindow(_pages, _settings.Current.FirstRunCompleted, page);
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
        if (command == TrayCommand.Open)
        {
            OpenSettings();
        }
        else if (command == TrayCommand.OpenTracking)
        {
            OpenSettings(typeof(TrackingPage));
        }
        else if (command == TrayCommand.Exit)
        {
            Exit();
        }
        else
        {
            _trayMenu.Handle(command);
        }
    }

    private void Exit()
    {
        _window?.CloseForExit();
        ExitRequested?.Invoke();
    }
}
