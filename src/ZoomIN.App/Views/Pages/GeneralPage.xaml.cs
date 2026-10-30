using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ZoomIN.App.ViewModels;

namespace ZoomIN.App.Views.Pages;

public sealed partial class GeneralPage : Page
{
    public GeneralPage()
    {
        InitializeComponent();
    }

    public GeneralViewModel? Vm { get; private set; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        Vm = new GeneralViewModel((PageContext)e.Parameter);
        Bindings.Update();
    }
}
