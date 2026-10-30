using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using ZoomIN.App.Composition;
using ZoomIN.App.Lifecycle;
using ZoomIN.App.Views;
using ZoomIN.App.Zoom;
using ZoomIN.Core.Diagnostics;

namespace ZoomIN.App;

public partial class App : Application
{
    private readonly ServiceProvider _services;
    private MainWindow? _mainWindow;

    public App()
    {
        InitializeComponent();
        _services = ServiceRegistration.Build();
        UnhandledException += (_, e) =>
            _services.GetRequiredService<ILog>().Error("Unhandled UI exception", e.Exception);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var log = _services.GetRequiredService<ILog>();
        log.Info("ZoomIN started");
        WatchdogStarter.Start(log);
        _services.GetRequiredService<ZoomController>().Initialize();

        _mainWindow = _services.GetRequiredService<MainWindow>();

        // Disposing the container stops the render thread and releases the hotkeys.
        _mainWindow.Closed += (_, _) => _services.Dispose();
        _mainWindow.Activate();
    }
}
