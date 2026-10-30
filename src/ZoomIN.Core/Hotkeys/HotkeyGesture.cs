namespace ZoomIN.Core.Hotkeys;

/// <summary>A global keyboard shortcut, e.g. Strg+Alt+Z (ADR-0008).</summary>
/// <param name="Modifiers">Modifier keys.</param>
/// <param name="VirtualKey">Windows virtual-key code of the main key (e.g. 0x5A for Z).</param>
public readonly record struct HotkeyGesture(HotkeyModifiers Modifiers, int VirtualKey)
{
    public static HotkeyGesture ToggleZoom { get; } =
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x5A);
}
