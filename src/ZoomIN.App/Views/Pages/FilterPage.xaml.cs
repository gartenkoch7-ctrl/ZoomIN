using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ZoomIN.App.ViewModels;

namespace ZoomIN.App.Views.Pages;

public sealed partial class FilterPage : Page
{
    public FilterPage()
    {
        InitializeComponent();
    }

    private PageServices? _services;

    public FilterViewModel? Vm { get; private set; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _services = (PageServices)e.Parameter;
        Vm = _services.Create<FilterViewModel>();
        Bindings.Update();
    }

    // Detail page of the filter per window (ADR-0020); "Back" there returns here.
    private void OnWindowFilter(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(WindowFilterPage), _services);

    // Detail page of the own colour schemes (M18); "Back" there returns here.
    private void OnCustomSchemes(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(CustomSchemesPage), _services);

    private void OnReset(object sender, RoutedEventArgs e) => Vm?.Reset();
}
