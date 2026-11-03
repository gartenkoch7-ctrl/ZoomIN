using System.ComponentModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using ZoomIN.Setup.Localization;

namespace ZoomIN.Setup.Wizard;

public sealed partial class SetupWindow : Window
{
    private const float ZoomStep = 0.25f;

    public SetupWindow()
    {
        InitializeComponent();
        Title = SetupText.Get("Title");
        AppWindow.Resize(new SizeInt32(760, 640));
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "ZoomIN.ico"));

        // Like the app's settings window: follow the Windows light/dark mode in the title bar too.
        AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;

        Vm.TextsChanged += OnTextsChanged;
        Vm.PropertyChanged += OnViewModelChanged;
    }

    public SetupViewModel Vm { get; } = new();

    public static Visibility On(int step, int visibleStep) => step == visibleStep ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility OnStatusStep(int step) => step is 1 or 3 ? Visibility.Visible : Visibility.Collapsed;

    public static bool Not(bool value) => !value;

    private void OnTextsChanged()
    {
        Title = SetupText.Get("Title");
        Bindings.Update();
    }

    // WinUI does not announce LiveSetting changes by itself; the app must raise the event.
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        var element = e.PropertyName switch
        {
            nameof(SetupViewModel.StepTitle) => StepTitleText,
            nameof(SetupViewModel.Status) => (FrameworkElement)StatusText,
            _ => null,
        };
        if (element is not null)
        {
            DispatcherQueue.TryEnqueue(() =>
                FrameworkElementAutomationPeer.FromElement(element)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged));
        }

        if (e.PropertyName == nameof(SetupViewModel.Step))
        {
            // Keyboard users continue where the next action is; the heading is announced.
            DispatcherQueue.TryEnqueue(() => NextButton.Focus(FocusState.Programmatic));
        }
    }

    private void OnBack(object sender, RoutedEventArgs e) => Vm.Back();

    private async void OnNext(object sender, RoutedEventArgs e)
    {
        if (await Vm.NextAsync())
        {
            Close();
        }
    }

    private async void OnRefresh(object sender, RoutedEventArgs e) => await Vm.RefreshAsync();

    private void OnZoomIn(object sender, object e) => SetZoom(Zoomer.ZoomFactor + ZoomStep, e);

    private void OnZoomOut(object sender, object e) => SetZoom(Zoomer.ZoomFactor - ZoomStep, e);

    private void OnZoomReset(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs e) => SetZoom(1, e);

    private void SetZoom(float factor, object args)
    {
        Zoomer.ChangeView(null, null, Math.Clamp(factor, Zoomer.MinZoomFactor, Zoomer.MaxZoomFactor));
        if (args is KeyboardAcceleratorInvokedEventArgs accelerator)
        {
            accelerator.Handled = true;
        }
    }
}
