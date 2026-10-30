using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage.Pickers;
using ZoomIN.App.ViewModels;

namespace ZoomIN.App.Views.Pages;

public sealed partial class ProfilesPage : Page
{
    private PageContext? _context;

    public ProfilesPage()
    {
        InitializeComponent();
    }

    public ProfilesViewModel? Vm { get; private set; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _context = (PageContext)e.Parameter;
        Vm = new ProfilesViewModel(_context);
        Bindings.Update();
    }

    private void OnActivate(object sender, RoutedEventArgs e) => Vm?.Activate();

    private void OnDelete(object sender, RoutedEventArgs e) => Vm?.Delete();

    private void OnCreate(object sender, RoutedEventArgs e) => Vm?.Create();

    private void OnRename(object sender, RoutedEventArgs e) => Vm?.Rename();

    private async void OnExport(object sender, RoutedEventArgs e)
    {
        if (Vm?.Selected is not { } name)
        {
            return;
        }

        var picker = new FileSavePicker { SuggestedFileName = name, SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeChoices.Add("ZoomIN", [".json"]);
        InitializeWithWindow(picker);
        if (await picker.PickSaveFileAsync() is { } file)
        {
            Vm.Export(file.Path);
        }
    }

    private async void OnImport(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeFilter.Add(".json");
        InitializeWithWindow(picker);
        if (await picker.PickSingleFileAsync() is { } file)
        {
            Vm?.Import(file.Path);
        }
    }

    // Unpackaged WinUI apps must tell file pickers which window owns them.
    private void InitializeWithWindow(object picker) =>
        WinRT.Interop.InitializeWithWindow.Initialize(picker, _context!.WindowHandle());
}
