using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Settings;
using ZoomIN.Core.Pointer;

namespace ZoomIN.App.ViewModels;

/// <summary>
/// Card "Bewegung begrenzen" on the pointer page (A6): mouse buffer, axis lock and its key.
/// Global settings: they apply at once and stay when the profile changes.
/// </summary>
public sealed partial class PointerConstraintViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly bool _ready;

    public PointerConstraintViewModel(SettingsService settings)
    {
        _settings = settings;
        var options = Options(settings);
        BufferIndex = (int)options.Buffer;
        AxisLock = options.AxisLock;
        KeyIndex = (int)options.AxisLockKey;
        _ready = true;
    }

    [ObservableProperty]
    public partial int BufferIndex { get; set; }

    [ObservableProperty]
    public partial bool AxisLock { get; set; }

    [ObservableProperty]
    public partial int KeyIndex { get; set; }

    partial void OnBufferIndexChanged(int value)
    {
        if (Enum.IsDefined((EdgeBufferLevel)value))
        {
            Change(options => options with { Buffer = (EdgeBufferLevel)value });
        }
        else if (_ready)
        {
            // The combo box reports -1 while its items are replaced: show the stored level again.
            BufferIndex = (int)Options(_settings).Buffer;
        }
    }

    partial void OnAxisLockChanged(bool value) => Change(options => options with { AxisLock = value });

    partial void OnKeyIndexChanged(int value)
    {
        if (Enum.IsDefined((AxisLockKey)value))
        {
            Change(options => options with { AxisLockKey = (AxisLockKey)value });
        }
        else if (_ready)
        {
            KeyIndex = (int)Options(_settings).AxisLockKey;
        }
    }

    private static PointerConstraintOptions Options(SettingsService settings) =>
        PointerConstraintOptions.Normalize(settings.Current.PointerConstraint);

    // Only the changed field, applied to the stored options.
    private void Change(Func<PointerConstraintOptions, PointerConstraintOptions> change)
    {
        if (_ready)
        {
            _settings.Update(s => s with { PointerConstraint = change(PointerConstraintOptions.Normalize(s.PointerConstraint)) });
        }
    }
}
