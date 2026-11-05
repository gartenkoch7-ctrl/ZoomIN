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

    private PageContext? _context;

    public FilterViewModel? Vm { get; private set; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _context = (PageContext)e.Parameter;
        Vm = new FilterViewModel(_context);
        Bindings.Update();
    }

    // Detail page of the filter per window (ADR-0020); "Back" there returns here.
    private void OnWindowFilter(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(WindowFilterPage), _context);

    private void OnReset(object sender, RoutedEventArgs e) => Vm?.Reset();
}
