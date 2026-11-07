using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ZoomIN.App.ViewModels;

namespace ZoomIN.App.Views.Pages;

public sealed partial class FilterPage : Page
{
    public FilterPage()
    {
        InitializeComponent();
    }

    public FilterViewModel? Vm { get; private set; }

    public ShareFilterViewModel? ShareVm { get; private set; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        Vm = new FilterViewModel((PageContext)e.Parameter);
        ShareVm = new ShareFilterViewModel((PageContext)e.Parameter);
        Bindings.Update();
    }

    private void OnReset(object sender, RoutedEventArgs e) => Vm?.Reset();
}
