namespace ZoomIN.Core.Hotkeys;

/// <summary>Checks for the hotkey editor (ADR-0008).</summary>
public static class HotkeyRules
{
    // Keys that produce a character with AltGr (= Ctrl+Alt) on a German keyboard:
    // Q @, E €, M µ, 2 ², 3 ³, 7 {, 8 [, 9 ], 0 }, ß \, + ~, < |.
    private static readonly HashSet<int> s_altGrKeys = [0x51, 0x45, 0x4D, 0x32, 0x33, 0x37, 0x38, 0x39, 0x30, 0xDB, 0xBB, 0xE2];

    /// <summary>A global shortcut needs Ctrl, Alt or Win; Shift alone would block typing.</summary>
    public static bool HasModifier(HotkeyGesture gesture) =>
        (gesture.Modifiers & (HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Windows)) != 0;

    /// <summary>Ctrl+Alt+key where the key types a character with AltGr: registering it would block that character.</summary>
    public static bool BlocksAltGrCharacter(HotkeyGesture gesture) =>
        (gesture.Modifiers & (HotkeyModifiers.Control | HotkeyModifiers.Alt)) == (HotkeyModifiers.Control | HotkeyModifiers.Alt)
        && (gesture.Modifiers & HotkeyModifiers.Windows) == 0
        && s_altGrKeys.Contains(gesture.VirtualKey);

    /// <summary>Shift, Ctrl, Alt and Win alone are not a main key.</summary>
    public static bool IsModifierKey(int virtualKey) =>
        virtualKey is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or >= 0xA0 and <= 0xA5;
}
