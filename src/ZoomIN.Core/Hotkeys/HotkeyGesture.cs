namespace ZoomIN.Core.Hotkeys;

/// <summary>A global keyboard shortcut, e.g. Strg+Alt+Z (ADR-0008).</summary>
/// <param name="Modifiers">Modifier keys.</param>
/// <param name="VirtualKey">Windows virtual-key code of the main key (e.g. 0x5A for Z).</param>
public readonly record struct HotkeyGesture(HotkeyModifiers Modifiers, int VirtualKey)
{
    private const HotkeyModifiers CtrlAlt = HotkeyModifiers.Control | HotkeyModifiers.Alt;

    public static HotkeyGesture ToggleZoom { get; } = new(CtrlAlt, 0x5A); // Z

    public static HotkeyGesture ZoomIn { get; } = new(CtrlAlt, 0x21); // Page Up

    public static HotkeyGesture ZoomOut { get; } = new(CtrlAlt, 0x22); // Page Down
}
