namespace ZoomIN.Core.Hotkeys;

/// <summary>A global keyboard shortcut, e.g. Strg+Alt+Z (ADR-0008).</summary>
/// <param name="Modifiers">Modifier keys.</param>
/// <param name="VirtualKey">Windows virtual-key code of the main key (e.g. 0x5A for Z).</param>
public readonly record struct HotkeyGesture(HotkeyModifiers Modifiers, int VirtualKey)
{
    private const HotkeyModifiers CtrlAlt = HotkeyModifiers.Control | HotkeyModifiers.Alt;
    private const HotkeyModifiers CtrlAltShift = CtrlAlt | HotkeyModifiers.Shift;

    public static HotkeyGesture ToggleZoom { get; } = new(CtrlAlt, 0x5A); // Z

    public static HotkeyGesture ZoomIn { get; } = new(CtrlAlt, 0x21); // Page Up

    public static HotkeyGesture ZoomOut { get; } = new(CtrlAlt, 0x22); // Page Down

    public static HotkeyGesture Fullscreen { get; } = new(CtrlAlt, 0x46); // F

    public static HotkeyGesture Lens { get; } = new(CtrlAlt, 0x4C); // L

    public static HotkeyGesture Docked { get; } = new(CtrlAlt, 0x44); // D

    public static HotkeyGesture ToggleFilter { get; } = new(CtrlAlt, 0x43); // C

    public static HotkeyGesture NextFilter { get; } = new(CtrlAltShift, 0x43); // Shift+C

    public static HotkeyGesture Locate { get; } = new(CtrlAlt, 0x53); // S

    // Ctrl+Alt+arrows rotate the screen on some Intel drivers, hence Shift (ADR-0008).
    public static HotkeyGesture LensWider { get; } = new(CtrlAltShift, 0x27); // Right

    public static HotkeyGesture LensNarrower { get; } = new(CtrlAltShift, 0x25); // Left

    public static HotkeyGesture LensTaller { get; } = new(CtrlAltShift, 0x26); // Up

    public static HotkeyGesture LensShorter { get; } = new(CtrlAltShift, 0x28); // Down
}
