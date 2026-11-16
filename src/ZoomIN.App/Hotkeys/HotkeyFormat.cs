using ZoomIN.App.Localization;
using ZoomIN.Core.Hotkeys;

namespace ZoomIN.App.Hotkeys;

/// <summary>Readable shortcut names in the UI language, e.g. "Strg+Alt+Z" / "Ctrl+Alt+Z".</summary>
public static class HotkeyFormat
{
    public static string Describe(HotkeyGesture gesture) =>
        gesture.VirtualKey == 0 ? Text.Get("HotkeyNone") : DescribeAssigned(gesture);

    private static string DescribeAssigned(HotkeyGesture gesture)
    {
        var parts = new List<string>();
        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            parts.Add(Text.IsGerman ? "Strg" : "Ctrl");
        }

        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add(Text.IsGerman ? "Umschalt" : "Shift");
        }

        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(Key(gesture.VirtualKey));
        return string.Join("+", parts);
    }

    public static string Key(int virtualKey) => virtualKey switch
    {
        >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A => ((char)virtualKey).ToString(),
        >= 0x70 and <= 0x87 => "F" + (virtualKey - 0x6F),
        >= 0x60 and <= 0x69 => "Num " + (virtualKey - 0x60),
        0x21 => Text.IsGerman ? "Bild↑" : "Page Up",
        0x22 => Text.IsGerman ? "Bild↓" : "Page Down",
        0x23 => Text.IsGerman ? "Ende" : "End",
        0x24 => "Pos1",
        0x25 => "←",
        0x26 => "↑",
        0x27 => "→",
        0x28 => "↓",
        0x2D => Text.IsGerman ? "Einfg" : "Insert",
        0x2E => Text.IsGerman ? "Entf" : "Delete",
        0x20 => Text.IsGerman ? "Leertaste" : "Space",
        0xBB => "+",
        0xBD => "-",
        _ => $"0x{virtualKey:X2}",
    };
}
