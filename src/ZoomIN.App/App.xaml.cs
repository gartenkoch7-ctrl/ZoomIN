using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using ZoomIN.App.Composition;
using ZoomIN.App.Views;
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
        _services.GetRequiredService<ILog>().Info("ZoomIN started");
        _mainWindow = _services.GetRequiredService<MainWindow>();
        _mainWindow.Activate();
    }
}
