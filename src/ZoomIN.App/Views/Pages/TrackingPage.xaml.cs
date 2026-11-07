using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ZoomIN.App.ViewModels;

namespace ZoomIN.App.Views.Pages;

public sealed partial class TrackingPage : Page
{
    public TrackingPage()
    {
        InitializeComponent();
    }

    public TrackingViewModel? Vm { get; private set; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        Vm = ((PageServices)e.Parameter).Create<TrackingViewModel>();
        Bindings.Update();
    }
}
