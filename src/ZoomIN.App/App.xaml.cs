using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using ZoomIN.App.Composition;
using ZoomIN.App.Lifecycle;
using ZoomIN.Core.Settings;
using ZoomIN.Platform.SecureDesktop;

namespace ZoomIN.App;

/// <summary>
/// Tray application (M7): no window is required to keep running; only "Exit" ends the process
/// (exit code 0, so the watchdog does not restart it).
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;
    private ILogger<App>? _log;
    private EaseOfAccessNotifier? _easeOfAccess;

    public App()
    {
        InitializeComponent();
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;
        UnhandledException += (_, e) => _log?.UnhandledUiException(e.Exception);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var telemetry = Program.Telemetry!;
        _services = ServiceRegistration.Build(telemetry);
        _log = _services.GetRequiredService<ILogger<App>>();

        // The user's diagnostics level is known only once the settings are loaded (ADR-0019).
        telemetry.Apply(_services.GetRequiredService<AppSettings>().Telemetry());
        WatchdogStarter.Start(_log);

        var shell = _services.GetRequiredService<AppShell>();
        shell.ExitRequested += ExitApp;
        shell.Start();
        Program.Instance?.ListenForOpenRequests(shell.OpenSettings);
        _easeOfAccess = EaseOfAccessNotifier.Create(_log);
        _easeOfAccess.NotifyStarted();

        Program.StartOperation?.Dispose();
        var started = (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMilliseconds;
        _log.Started(started);
    }

    private void ExitApp()
    {
        // Disposing stops rendering, releases hotkeys/AppBar/pointer and saves the settings.
        _log?.Exits();
        _easeOfAccess?.NotifyExiting();
        _services?.Dispose();
        Exit();
    }
}
