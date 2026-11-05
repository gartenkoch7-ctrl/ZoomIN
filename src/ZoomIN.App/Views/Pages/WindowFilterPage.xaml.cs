using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ZoomIN.App.ViewModels;

namespace ZoomIN.App.Views.Pages;

public sealed partial class WindowFilterPage : Page
{
    public WindowFilterPage()
    {
        InitializeComponent();
    }

    public WindowFilterViewModel? Vm { get; private set; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        Vm = new WindowFilterViewModel((PageContext)e.Parameter);
        Bindings.Update();
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: WindowRuleRow row })
        {
            Vm?.Remove(row);
        }
    }

    private void OnAdd(object sender, RoutedEventArgs e) => Vm?.Add();

    private void OnResetConfirmed(object sender, RoutedEventArgs e)
    {
        ResetFlyout.Hide();
        Vm?.ResetAll();
    }
}
