using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using ZoomIN.App.ViewModels;

namespace ZoomIN.App.Views.Sections;

/// <summary>Installed version, channel, automatic check and the version list. Shown on the "General" page.</summary>
public sealed partial class UpdatesSection : UserControl
{
    public UpdatesSection()
    {
        InitializeComponent();
    }

    public UpdatesViewModel? Vm { get; private set; }

    /// <summary>Called by the hosting page on every navigation to it; fetches the versions while the list is empty.</summary>
    public async Task LoadAsync(PageServices services)
    {
        Unload();
        Vm = services.Create<UpdatesViewModel>();
        Vm.PropertyChanged += OnViewModelChanged;
        Bindings.Update();

        // Fetched while the list is empty; "Check now" fetches again.
        if (Vm.Versions.Count == 0)
        {
            await Vm.RefreshAsync();
        }
    }

    /// <summary>Called by the hosting page when it is left.</summary>
    public void Unload()
    {
        if (Vm is not null)
        {
            Vm.PropertyChanged -= OnViewModelChanged;
        }
    }

    // WinUI does not announce LiveSetting changes by itself; the control must raise the event.
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
