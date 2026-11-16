using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using ZoomIN.App.ViewModels;
using ZoomIN.Core.Pointer;

namespace ZoomIN.App.Views.Pages;

public sealed partial class CustomSchemesPage : Page
{
    public CustomSchemesPage()
    {
        InitializeComponent();

        // Closing the settings window ends the page without a navigation; the open edit is logged then.
        Unloaded += (_, _) => Vm?.Finish();
    }

    public CustomSchemesViewModel? Vm { get; private set; }

    /// <summary>Brush of a "#RRGGBB" colour, for the preview swatches.</summary>
    public static SolidColorBrush BrushOf(string hex)
    {
        var c = RgbColor.ParseOrDefault(hex, RgbColor.Black);
        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, c.R, c.G, c.B));
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        Vm = ((PageServices)e.Parameter).Create<CustomSchemesViewModel>();
        Vm.PropertyChanged += OnVmChanged;
        Bindings.Update();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        if (Vm is { } vm)
        {
            vm.PropertyChanged -= OnVmChanged;
            vm.Finish();
        }

        base.OnNavigatedFrom(e);
    }

    // Screen readers announce the result of create and delete (live region). Queued, so the
    // binding has put the new text into the block before the event asks for it.
    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CustomSchemesViewModel.Status))
        {
            DispatcherQueue.TryEnqueue(() =>
                FrameworkElementAutomationPeer.FromElement(StatusText)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged));
        }
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        Vm?.Add();
        FocusName();
    }

    private void OnEdit(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: SchemeRow row })
        {
            Vm?.Edit(row.Id);
            FocusName();
        }
    }

    // Focus goes into the new row: the first colour to pick. With the third row the button turns
    // off, and a disabled button would drop the focus to the top of the window.
    private void OnAddReplacement(object sender, RoutedEventArgs e)
    {
        if (Vm?.Editor is not { } editor)
        {
            return;
        }

        editor.AddReplacement();
        var last = editor.Replacements.Count - 1;
        DispatcherQueue.TryEnqueue(() =>
        {
            // The new row's container may not exist before layout has run.
            ReplacementList.UpdateLayout();
            if (ReplacementList.ContainerFromIndex(last) is DependencyObject row
                && FocusManager.FindFirstFocusableElement(row) is Control first)
            {
                first.Focus(FocusState.Programmatic);
            }
        });
    }

    private void OnRemoveReplacement(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ReplacementRow row })
        {
            Vm?.Editor?.RemoveReplacement(row);

            // The rows are rebuilt and the pressed button is gone; keep keyboard focus nearby.
            DispatcherQueue.TryEnqueue(() => AddReplacementButton.Focus(FocusState.Programmatic));
        }
    }

    private void OnUse(object sender, RoutedEventArgs e) => Vm?.ShowEditing();

    private void OnDeleteConfirmed(object sender, RoutedEventArgs e)
    {
        DeleteFlyout.Hide();
        Vm?.DeleteEditing();

        // The editor is gone; keyboard focus would fall back to the top of the window.
        DispatcherQueue.TryEnqueue(() => CreateButton.Focus(FocusState.Programmatic));
    }

    // Keyboard users land in the editor they just opened instead of staying in the list.
    private void FocusName()
    {
        if (Vm?.IsEditing == true)
        {
            DispatcherQueue.TryEnqueue(() => NameBox.Focus(FocusState.Programmatic));
        }
    }
}
