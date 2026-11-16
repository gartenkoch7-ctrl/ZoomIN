using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Settings;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Pointer;

namespace ZoomIN.App.ViewModels;

/// <summary>
/// Frozen monitors (G2) on the page "Zoom &amp; Modi": live or still image, and the frame colour.
/// Shared by all profiles; applies to the next freeze and is saved at once.
/// </summary>
public sealed partial class FreezeViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly bool _ready;

    public FreezeViewModel(SettingsService settings)
    {
        _settings = settings;
        var current = settings.Current;
        // A hand-edited file may hold any number: the combo box has two entries.
        ContentIndex = Enum.IsDefined(current.FrozenContent) ? (int)current.FrozenContent : (int)FrozenContent.Live;
        FrameColor = RgbColor.ParseOrDefault(current.FrozenFrameColor, RgbColor.Cyan).ToHex();
        _ready = true;
    }

    [ObservableProperty]
    public partial int ContentIndex { get; set; }

    [ObservableProperty]
    public partial string FrameColor { get; set; } = string.Empty;

    partial void OnContentIndexChanged(int value)
    {
        // The combo box reports -1 while its items are replaced.
        if (_ready && Enum.IsDefined((FrozenContent)value))
        {
            _settings.Update(s => s with { FrozenContent = (FrozenContent)value });
        }
    }

    partial void OnFrameColorChanged(string value)
    {
        if (_ready)
        {
            var color = RgbColor.ParseOrDefault(value, RgbColor.Cyan).ToHex();
            _settings.Update(s => s with { FrozenFrameColor = color });
        }
    }
}
