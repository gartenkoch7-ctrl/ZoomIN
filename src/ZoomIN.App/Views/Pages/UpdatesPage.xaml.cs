using Microsoft.UI.Xaml;
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
        Vm = new UpdatesViewModel((PageContext)e.Parameter);
        Bindings.Update();

        // Fetched while the list is empty; "Check now" fetches again.
        if (Vm.Versions.Count == 0)
        {
            await Vm.RefreshAsync();
        }
    }

    private async void OnInstall(object sender, RoutedEventArgs e) => await (Vm?.InstallAsync() ?? Task.CompletedTask);

    private async void OnRefresh(object sender, RoutedEventArgs e) => await (Vm?.RefreshAsync() ?? Task.CompletedTask);
}
