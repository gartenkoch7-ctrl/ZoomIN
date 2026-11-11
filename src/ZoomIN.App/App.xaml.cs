using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using ZoomIN.App.AgentDebug;
using ZoomIN.App.Composition;
using ZoomIN.App.Lifecycle;
using ZoomIN.App.Settings;
using ZoomIN.Core.Diagnostics;
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

        // Phases as child operations of zoomin.app.start (startup measurement, M14). The parent is
        // passed explicitly: the WinUI dispatcher does not promise to keep Activity.Current.
        var parent = Program.StartOperation?.Context ?? default;
        using (Telemetry.Source.StartActivity("zoomin.app.start.services", ActivityKind.Internal, parent))
        {
            _services = ServiceRegistration.Build(telemetry, Program.Folders);
            _log = _services.GetRequiredService<ILogger<App>>();

            // The user's diagnostics level is known only once the settings are loaded (ADR-0019);
            // changes on the diagnostics page take effect at once (Apply ignores unrelated changes).
            telemetry.Apply(_services.GetRequiredService<AppSettings>().Telemetry());
            var settings = _services.GetRequiredService<SettingsService>();
            settings.Changed += () => telemetry.Apply(settings.Current.Telemetry());
            _services.GetRequiredService<IWatchdogLauncher>().Start();
        }

        using (Telemetry.Source.StartActivity("zoomin.app.start.shell", ActivityKind.Internal, parent))
        {
            // Finer phases (M14): building the object graph vs. starting engine, tray and notices.
            AppShell shell;
            using (Telemetry.Source.StartActivity("zoomin.app.start.shell.resolve"))
            {
                shell = _services.GetRequiredService<AppShell>();
            }

            shell.ExitRequested += ExitApp;
            shell.Start();
            using (Telemetry.Source.StartActivity("zoomin.app.start.shell.notify"))
            {
                Program.Instance?.ListenForOpenRequests(shell.OpenSettings);
                _services.GetRequiredService<AgentDebugHost>().Start();
                _easeOfAccess = EaseOfAccessNotifier.Create(_log);
                _easeOfAccess.NotifyStarted();
            }
        }

        Program.StartOperation?.Dispose();
        using var process = Process.GetCurrentProcess();
        _log.Started((DateTime.Now - process.StartTime).TotalMilliseconds);
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
