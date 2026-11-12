using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace ZoomIN.Platform.Input;

/// <summary>
/// Keys that never count as typing, shared by the activity hook and the hold key hook (M18), so
/// "typed" for the pointer and "another key" for the hold key cannot drift apart.
/// </summary>
internal static class ModifierKeys
{
    public static bool Is(VIRTUAL_KEY key) => key is VIRTUAL_KEY.VK_SHIFT or VIRTUAL_KEY.VK_CONTROL or VIRTUAL_KEY.VK_MENU
        or VIRTUAL_KEY.VK_LSHIFT or VIRTUAL_KEY.VK_RSHIFT or VIRTUAL_KEY.VK_LCONTROL or VIRTUAL_KEY.VK_RCONTROL
        or VIRTUAL_KEY.VK_LMENU or VIRTUAL_KEY.VK_RMENU or VIRTUAL_KEY.VK_LWIN or VIRTUAL_KEY.VK_RWIN or VIRTUAL_KEY.VK_CAPITAL;
}
