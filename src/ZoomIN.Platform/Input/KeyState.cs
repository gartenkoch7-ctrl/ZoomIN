using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using ZoomIN.Core.Hotkeys;

namespace ZoomIN.Platform.Input;

/// <summary>Virtual-key codes of the modifiers and the physical key state (interop).</summary>
internal static class KeyState
{
    public const int Control = (int)VIRTUAL_KEY.VK_CONTROL;
    public const int Alt = (int)VIRTUAL_KEY.VK_MENU;
    public const int Shift = (int)VIRTUAL_KEY.VK_SHIFT;
    public const int LeftWindows = (int)VIRTUAL_KEY.VK_LWIN;
    public const int RightWindows = (int)VIRTUAL_KEY.VK_RWIN;

    public static bool IsDown(int virtualKey) => PInvoke.GetAsyncKeyState(virtualKey) < 0;

    /// <summary>The modifier a key (also its left or right variant) stands for, or None.</summary>
    public static HotkeyModifiers ModifierOf(int virtualKey) => (VIRTUAL_KEY)virtualKey switch
    {
        VIRTUAL_KEY.VK_CONTROL or VIRTUAL_KEY.VK_LCONTROL or VIRTUAL_KEY.VK_RCONTROL => HotkeyModifiers.Control,
        VIRTUAL_KEY.VK_MENU or VIRTUAL_KEY.VK_LMENU or VIRTUAL_KEY.VK_RMENU => HotkeyModifiers.Alt,
        VIRTUAL_KEY.VK_SHIFT or VIRTUAL_KEY.VK_LSHIFT or VIRTUAL_KEY.VK_RSHIFT => HotkeyModifiers.Shift,
        VIRTUAL_KEY.VK_LWIN or VIRTUAL_KEY.VK_RWIN => HotkeyModifiers.Windows,
        _ => HotkeyModifiers.None,
    };
}
