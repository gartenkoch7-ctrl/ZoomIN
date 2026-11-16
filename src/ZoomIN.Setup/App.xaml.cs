using Microsoft.UI.Xaml;
using ZoomIN.Setup.Wizard;

namespace ZoomIN.Setup;

public partial class App : Application
{
    private SetupWindow? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new SetupWindow();
        _window.Activate();
    }
}
