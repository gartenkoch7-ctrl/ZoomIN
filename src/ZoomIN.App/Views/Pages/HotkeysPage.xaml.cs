using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;
using Windows.UI.Core;
using ZoomIN.App.ViewModels;
using ZoomIN.Core.Hotkeys;

namespace ZoomIN.App.Views.Pages;

public sealed partial class HotkeysPage : Page
{
    public HotkeysPage()
    {
        InitializeComponent();
    }

    public HotkeysViewModel? Vm { get; private set; }

    public bool HasMessage => !string.IsNullOrEmpty(Vm?.Message);

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        Vm = ((PageServices)e.Parameter).Create<HotkeysViewModel>();
        Vm.PropertyChanged += (_, _) => Bindings.Update();
        Bindings.Update();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        // Leaving the page while recording must not leave the shortcuts released.
        if (Vm?.IsRecording == true)
        {
            Vm.Record(0x1B, HotkeyModifiers.None);
        }
    }

    private void OnChangeClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: HotkeyRow row })
        {
            Vm?.StartRecording(row);
        }
    }

    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (Vm?.IsRecording != true)
        {
            return;
        }

        e.Handled = Vm.Record((int)e.Key, CurrentModifiers());
    }

    private static HotkeyModifiers CurrentModifiers()
    {
        var modifiers = HotkeyModifiers.None;
        if (IsDown(VirtualKey.Control))
        {
            modifiers |= HotkeyModifiers.Control;
        }

        if (IsDown(VirtualKey.Menu))
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        if (IsDown(VirtualKey.Shift))
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        if (IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows))
        {
            modifiers |= HotkeyModifiers.Windows;
        }

        return modifiers;
    }

    private static bool IsDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);
}
