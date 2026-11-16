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

    /// <summary>Explains what the shortcut does where its name is not enough (e.g. the overview).</summary>
    public string Hint { get; init; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Description))]
    public partial string Warning { get; set; } = string.Empty;

    /// <summary>Shown under the name: the warning if there is one, otherwise the hint.</summary>
    public string Description => Warning.Length > 0 ? Warning : Hint;
}
