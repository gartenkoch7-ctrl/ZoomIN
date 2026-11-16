using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ZoomIN.App.ViewModels;

namespace ZoomIN.App.Views.Pages;

public sealed partial class WelcomePage : Page
{
    public WelcomePage()
    {
        InitializeComponent();
    }

    public WelcomeViewModel? Vm { get; private set; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        Vm = ((PageServices)e.Parameter).Create<WelcomeViewModel>();
        Bindings.Update();
    }

    private void OnBack(object sender, RoutedEventArgs e) => Vm?.Back();

    private void OnNext(object sender, RoutedEventArgs e) => Vm?.Next();

    private void OnToggleFilter(object sender, RoutedEventArgs e) => Vm?.ToggleFilter();
}
