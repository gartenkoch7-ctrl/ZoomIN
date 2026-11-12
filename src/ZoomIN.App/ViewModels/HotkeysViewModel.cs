using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Hotkeys;
using ZoomIN.App.Localization;
using ZoomIN.App.Settings;
using ZoomIN.App.Zoom;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Tracking;

namespace ZoomIN.App.ViewModels;

/// <summary>
/// Shortcut editor (M7). While recording, all global shortcuts are released so the pressed
/// combination reaches the window; afterwards they are registered again.
/// </summary>
public sealed partial class HotkeysViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly HotkeyBindings _hotkeys;
    private readonly bool _ready;
    private HotkeyRow? _recording;

    public HotkeysViewModel(SettingsService settings, HotkeyBindings hotkeys)
    {
        _settings = settings;
        _hotkeys = hotkeys;
        var key = settings.Current.MarkerHoldKey;
        HoldKeyIndex = Enum.IsDefined(key) ? (int)key : 0;
        _ready = true;
        Refresh();
    }

    /// <summary>Key that shows highlights set to "only while held" (E1); shared by all profiles.</summary>
    [ObservableProperty]
    public partial int HoldKeyIndex { get; set; }

    public ObservableCollection<HotkeyRow> Rows { get; } = [];

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    public bool IsRecording => _recording is not null;

    public void StartRecording(HotkeyRow row)
    {
        _recording = row;
        _hotkeys.Suspend();
        row.Gesture = Text.Get(row.Binding.Optional ? "HotkeyClearHint" : "HotkeyPress");
        Message = string.Empty;
    }

    /// <summary>Called with each key press while recording; returns true when the key was used.</summary>
    public bool Record(int virtualKey, HotkeyModifiers modifiers)
    {
        if (_recording is not { } row || HotkeyRules.IsModifierKey(virtualKey))
        {
            return false;
        }

        // Esc cancels.
        if (virtualKey == 0x1B)
        {
            Finish(null);
            return true;
        }

        // Del alone removes an optional shortcut (it stays unassigned).
        if (virtualKey == 0x2E && modifiers == HotkeyModifiers.None && row.Binding.Optional)
        {
            Finish((row.Binding, default));
            return true;
        }

        var gesture = new HotkeyGesture(modifiers, virtualKey);
        if (!HotkeyRules.HasModifier(gesture))
        {
            Message = Text.Get("HotkeyNeedsModifier");
            return true;
        }

        if (HotkeyRules.BlocksAltGrCharacter(gesture))
        {
            Message = Text.Format("HotkeyAltGr", HotkeyFormat.Key(virtualKey));
            return true;
        }

        Finish((row.Binding, gesture));
        return true;
    }

    // −1 = no selection (the ComboBox unloads): not a choice. The constructor only shows the setting.
    partial void OnHoldKeyIndexChanged(int value)
    {
        var key = ComboChoice.Or(value, _settings.Current.MarkerHoldKey);
        if (_ready && key != _settings.Current.MarkerHoldKey)
        {
            _settings.Update(s => s with { MarkerHoldKey = key });
        }
    }

    private void Finish((HotkeyBinding Binding, HotkeyGesture Gesture)? change)
    {
        if (change is { } c)
        {
            _settings.Update(s => c.Binding.Set(s, c.Gesture));
        }

        _recording = null;
        _hotkeys.Register();
        Refresh();
    }

    private void Refresh()
    {
        var settings = _settings.Current;
        var unavailable = _hotkeys.Unavailable;
        Rows.Clear();
        foreach (var binding in HotkeyBinding.All)
        {
            var gesture = binding.Get(settings);
            Rows.Add(new HotkeyRow(binding, Text.Get(binding.TextKey))
            {
                Gesture = HotkeyFormat.Describe(gesture),
                Hint = binding.HintKey is { } hint ? Text.Get(hint) : string.Empty,
                Warning = unavailable.Contains(gesture) ? Text.Get("HotkeyUnavailable") : string.Empty,
            });
        }
    }
}
