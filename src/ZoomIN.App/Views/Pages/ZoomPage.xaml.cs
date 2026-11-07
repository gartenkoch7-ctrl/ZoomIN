using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ZoomIN.App.ViewModels;

namespace ZoomIN.App.Views.Pages;

public sealed partial class ZoomPage : Page
{
    public ZoomPage()
    {
        InitializeComponent();
    }

    public ZoomViewModel? Vm { get; private set; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        Vm = ((PageServices)e.Parameter).Create<ZoomViewModel>();
        Bindings.Update();
    }
}
