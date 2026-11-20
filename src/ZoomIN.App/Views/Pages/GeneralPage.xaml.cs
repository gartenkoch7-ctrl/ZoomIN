using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ZoomIN.App.ViewModels;

namespace ZoomIN.App.Views.Pages;

/// <summary>Language, start, profiles, updates and diagnostics (profiles and updates were own pages until 0.9.1).</summary>
public sealed partial class GeneralPage : Page
{
    public GeneralPage()
    {
        InitializeComponent();
    }

    public GeneralViewModel? Vm { get; private set; }

    public DiagnosticsViewModel? Diag { get; private set; }

    public DebugSnapshotViewModel? Snapshot { get; private set; }

    public AgentDebugViewModel? Agent { get; private set; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        var services = (PageServices)e.Parameter;
        Vm = services.Create<GeneralViewModel>();
        Diag = services.Create<DiagnosticsViewModel>();
        Snapshot = services.Create<DebugSnapshotViewModel>();
        Agent = services.Create<AgentDebugViewModel>();
        Bindings.Update();
        Profiles.Load(services);

        // Last: it may wait for the version list.
        await Updates.LoadAsync(services);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e) => Updates.Unload();
}
