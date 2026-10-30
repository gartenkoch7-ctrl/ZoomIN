using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Hotkeys;
using ZoomIN.App.Localization;
using ZoomIN.App.Views;
using ZoomIN.Core.Hotkeys;

namespace ZoomIN.App.ViewModels;

/// <summary>
/// Shortcut editor (M7). While recording, all global shortcuts are released so the pressed
/// combination reaches the window; afterwards they are registered again.
/// </summary>
public sealed partial class HotkeysViewModel : ObservableObject
{
    private readonly PageContext _context;
    private HotkeyRow? _recording;

    public HotkeysViewModel(PageContext context)
    {
        _context = context;
        Refresh();
    }

    public ObservableCollection<HotkeyRow> Rows { get; } = [];

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    public bool IsRecording => _recording is not null;

    public void StartRecording(HotkeyRow row)
    {
        _recording = row;
        _context.Controller.SuspendHotkeys();
        row.Gesture = Text.Get("HotkeyPress");
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

    private void Finish((HotkeyBinding Binding, HotkeyGesture Gesture)? change)
    {
        if (change is { } c)
        {
            _context.Settings.Update(s => c.Binding.Set(s, c.Gesture));
        }

        _recording = null;
        _context.Controller.ReloadHotkeys();
        Refresh();
    }

    private void Refresh()
    {
        var settings = _context.Settings.Current;
        var unavailable = _context.Controller.UnavailableHotkeys;
        Rows.Clear();
        foreach (var binding in HotkeyBinding.All)
        {
            var gesture = binding.Get(settings);
            Rows.Add(new HotkeyRow(binding, Text.Get(binding.TextKey))
            {
                Gesture = HotkeyFormat.Describe(gesture),
                Warning = unavailable.Contains(gesture) ? Text.Get("HotkeyUnavailable") : string.Empty,
            });
        }
    }
}
