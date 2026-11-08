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

    public DebugSnapshotViewModel? Snapshot { get; private set; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        var services = (PageServices)e.Parameter;
        Vm = services.Create<GeneralViewModel>();
        Diag = services.Create<DiagnosticsViewModel>();
        Snapshot = services.Create<DebugSnapshotViewModel>();
        Bindings.Update();
    }
}
