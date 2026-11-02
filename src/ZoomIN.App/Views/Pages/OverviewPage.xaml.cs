using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ZoomIN.App.ViewModels;

namespace ZoomIN.App.Views.Pages;

/// <summary>Start page (UI redesign): the most important switches at a glance.</summary>
public sealed partial class OverviewPage : Page
{
    private DispatcherQueueTimer? _timer;

    public OverviewPage()
    {
        InitializeComponent();
    }

    public OverviewViewModel? Vm { get; private set; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        Vm = new OverviewViewModel((PageContext)e.Parameter);
        Bindings.Update();

        // Hotkeys and the tray change the state too; the render threads never call into the UI.
        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(500);
        _timer.Tick += (_, _) => Vm.Refresh();
        _timer.Start();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e) => _timer?.Stop();

    private void OnZoomIn(object sender, RoutedEventArgs e)
    {
        Vm?.ZoomIn();
        Vm?.Refresh();
    }

    private void OnZoomOut(object sender, RoutedEventArgs e)
    {
        Vm?.ZoomOut();
        Vm?.Refresh();
    }

    private void OnLocate(object sender, RoutedEventArgs e) => Vm?.Locate();
}
