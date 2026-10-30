using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ZoomIN.App.ViewModels;

namespace ZoomIN.App.Views.Pages;

public sealed partial class ColorsPage : Page
{
    public ColorsPage()
    {
        InitializeComponent();
    }

    public ColorsViewModel? Vm { get; private set; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        Vm = new ColorsViewModel((PageContext)e.Parameter);
        Bindings.Update();
    }
}
