using CommunityToolkit.Mvvm.ComponentModel;

namespace ZoomIN.App.ViewModels;

/// <summary>
/// One colour of the scheme preview: before and after as "#RRGGBB", and what a screen reader says.
/// Updated in place on every edit, so a focused swatch keeps its focus.
/// </summary>
public sealed partial class PreviewSwatch(string before) : ObservableObject
{
    public string Before { get; } = before;

    [ObservableProperty]
    public partial string After { get; set; } = before;

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;
}
