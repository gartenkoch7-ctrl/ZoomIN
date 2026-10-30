using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Hotkeys;

namespace ZoomIN.App.ViewModels;

/// <summary>One line of the shortcut editor.</summary>
public sealed partial class HotkeyRow : ObservableObject
{
    public HotkeyRow(HotkeyBinding binding, string name)
    {
        Binding = binding;
        Name = name;
    }

    public HotkeyBinding Binding { get; }

    public string Name { get; }

    [ObservableProperty]
    public partial string Gesture { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Warning { get; set; } = string.Empty;
}
