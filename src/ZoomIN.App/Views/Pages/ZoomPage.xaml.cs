using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ZoomIN.App.ViewModels;

namespace ZoomIN.App.Views.Pages;

public sealed partial class ZoomPage : Page
{
    public ZoomPage()
    {
        InitializeComponent();
    }

    public ZoomViewModel? Vm { get; private set; }

    /// <summary>Zoom while moving windows (G1).</summary>
    public WindowDragViewModel? Drag { get; private set; }

    /// <summary>Frozen monitors (G2).</summary>
    public FreezeViewModel? Freeze { get; private set; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        var services = (PageServices)e.Parameter;
        Vm = services.Create<ZoomViewModel>();
        Drag = services.Create<WindowDragViewModel>();
        Freeze = services.Create<FreezeViewModel>();
        Bindings.Update();
    }
}
