using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using ZoomIN.App.Composition;
using ZoomIN.App.Lifecycle;
using ZoomIN.Core.Diagnostics;

namespace ZoomIN.App;

/// <summary>
/// Tray application (M7): no window is required to keep running; only "Exit" ends the process
/// (exit code 0, so the watchdog does not restart it).
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;

    public App()
    {
        InitializeComponent();
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;
        UnhandledException += (_, e) => _services?.GetRequiredService<ILog>().Error("Unhandled UI exception", e.Exception);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _services = ServiceRegistration.Build();
        var log = _services.GetRequiredService<ILog>();
        log.Info("ZoomIN started");
        WatchdogStarter.Start(log);

        var shell = _services.GetRequiredService<AppShell>();
        shell.ExitRequested += ExitApp;
        shell.Start();
        Program.Instance?.ListenForOpenRequests(shell.OpenSettings);
    }

    private void ExitApp()
    {
        // Disposing stops rendering, releases hotkeys/AppBar/pointer and saves the settings.
        _services?.GetRequiredService<ILog>().Info("ZoomIN exits");
        _services?.Dispose();
        Exit();
    }
}
