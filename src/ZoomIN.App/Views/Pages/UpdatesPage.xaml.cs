using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ZoomIN.App.ViewModels;

namespace ZoomIN.App.Views.Pages;

public sealed partial class UpdatesPage : Page
{
    public UpdatesPage()
    {
        InitializeComponent();
    }

    public UpdatesViewModel? Vm { get; private set; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        Vm = ((PageServices)e.Parameter).Create<UpdatesViewModel>();
        Vm.PropertyChanged += OnViewModelChanged;
        Bindings.Update();

        // Fetched while the list is empty; "Check now" fetches again.
        if (Vm.Versions.Count == 0)
        {
            await Vm.RefreshAsync();
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        if (Vm is not null)
        {
            Vm.PropertyChanged -= OnViewModelChanged;
        }
    }

    // WinUI does not announce LiveSetting changes by itself; the page must raise the event.
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UpdatesViewModel.Status))
        {
            DispatcherQueue.TryEnqueue(() =>
                FrameworkElementAutomationPeer.FromElement(StatusText)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged));
        }
    }

    private async void OnInstall(object sender, RoutedEventArgs e) => await (Vm?.InstallAsync() ?? Task.CompletedTask);

    private async void OnRefresh(object sender, RoutedEventArgs e) => await (Vm?.RefreshAsync() ?? Task.CompletedTask);
}
