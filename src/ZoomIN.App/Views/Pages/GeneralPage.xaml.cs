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

    public DiagnosticsViewModel? Diag { get; private set; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        var context = (PageContext)e.Parameter;
        Vm = new GeneralViewModel(context);
        Diag = new DiagnosticsViewModel(context.Settings);
        Bindings.Update();
    }
}
