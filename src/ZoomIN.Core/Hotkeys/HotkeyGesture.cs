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

    public static HotkeyGesture ToggleCrosshair { get; } = new(CtrlAlt, 0x4B); // K

    public static HotkeyGesture ToggleTracking { get; } = new(CtrlAlt, 0x54); // T

    public static HotkeyGesture NextProfile { get; } = new(CtrlAlt, 0x50); // P

    /// <summary>Overview (B2): tap = 1× with a frame, tap again = zoom in there; hold = while held.</summary>
    public static HotkeyGesture Overview { get; } = new(CtrlAlt, 0x4F); // O

    /// <summary>Freezes the monitor under the pointer, or unfreezes all if one is frozen (G2, M19).</summary>
    public static HotkeyGesture FreezeMonitor { get; } = new(CtrlAlt, 0x48); // H

    /// <summary>Moves the pointer to the next free monitor along the layout (G2, M19).</summary>
    public static HotkeyGesture NextMonitor { get; } = new(CtrlAlt, 0x4E); // N

    /// <summary>Ends ZoomIN on the secure desktop only (ADR-0016); Shift+Z, because Q types @ with AltGr.</summary>
    public static HotkeyGesture ExitSecureDesktop { get; } = new(CtrlAltShift, 0x5A); // Shift+Z

    // Ctrl+Alt+arrows rotate the screen on some Intel drivers, hence Shift (ADR-0008).
    public static HotkeyGesture LensWider { get; } = new(CtrlAltShift, 0x27); // Right

    public static HotkeyGesture LensNarrower { get; } = new(CtrlAltShift, 0x25); // Left

    public static HotkeyGesture LensTaller { get; } = new(CtrlAltShift, 0x26); // Up

    public static HotkeyGesture LensShorter { get; } = new(CtrlAltShift, 0x28); // Down
}
