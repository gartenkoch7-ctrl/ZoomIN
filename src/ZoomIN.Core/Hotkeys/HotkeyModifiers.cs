namespace ZoomIN.Core.Hotkeys;

/// <summary>Modifier keys. Values match the Win32 MOD_* flags so Platform can pass them through.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 0x1,
    Control = 0x2,
    Shift = 0x4,
    Windows = 0x8,
}
