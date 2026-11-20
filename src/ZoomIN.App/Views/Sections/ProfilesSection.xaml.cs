using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using ZoomIN.App.Lifecycle;
using ZoomIN.App.ViewModels;

namespace ZoomIN.App.Views.Sections;

/// <summary>Profiles: active profile, list, create, rename, import and export. Shown on the "General" page.</summary>
public sealed partial class ProfilesSection : UserControl
{
    private PageServices? _services;

    public ProfilesSection()
    {
        InitializeComponent();
    }

    public ProfilesViewModel? Vm { get; private set; }

    /// <summary>Called by the hosting page on every navigation to it.</summary>
    public void Load(PageServices services)
    {
        _services = services;
        Vm = services.Create<ProfilesViewModel>();
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
        WinRT.Interop.InitializeWithWindow.Initialize(picker, _services!.Get<IShellActions>().SettingsWindowHandle);
}
