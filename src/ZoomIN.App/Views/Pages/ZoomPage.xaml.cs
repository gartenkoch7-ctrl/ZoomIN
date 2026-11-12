using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ZoomIN.App.ViewModels;

namespace ZoomIN.App.Views.Pages;

public sealed partial class ZoomPage : Page
{
    public ZoomPage()
    {
        InitializeComponent();

        // Closing the settings window ends the page without a navigation.
        Unloaded += (_, _) => Islands?.Unwatch();
    }

    public ZoomViewModel? Vm { get; private set; }

    /// <summary>Zoom while moving windows (G1).</summary>
    public WindowDragViewModel? Drag { get; private set; }

    /// <summary>Islands (M19 B1).</summary>
    public IslandsViewModel? Islands { get; private set; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        var services = (PageServices)e.Parameter;
        Vm = services.Create<ZoomViewModel>();
        Drag = services.Create<WindowDragViewModel>();
        Islands = services.Create<IslandsViewModel>();
        Bindings.Update();

        // The edit mode, the shortcuts and profile switches change the islands on other threads.
        Islands.Watch(refresh => DispatcherQueue.TryEnqueue(() => refresh()));
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        Islands?.Unwatch();
        base.OnNavigatedFrom(e);
    }

    private void OnEditIslands(object sender, RoutedEventArgs e) => Islands?.Edit();

    private void OnRemoveIsland(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: IslandRow row })
        {
            Islands?.Remove(row);
        }
    }
}
