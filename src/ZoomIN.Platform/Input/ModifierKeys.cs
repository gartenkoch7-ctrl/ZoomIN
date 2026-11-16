using Windows.Win32.UI.Input.KeyboardAndMouse;
using ZoomIN.Core.Hotkeys;

namespace ZoomIN.Platform.Input;

/// <summary>
/// Keys that never count as typing, shared by the activity hook and the hold key hook (M18), so
/// "typed" for the pointer and "another key" for the hold key cannot drift apart. The modifiers are
/// those of <see cref="KeyState.ModifierOf"/>, plus Caps Lock.
/// </summary>
internal static class ModifierKeys
{
    public static bool Is(VIRTUAL_KEY key) =>
        key == VIRTUAL_KEY.VK_CAPITAL || KeyState.ModifierOf((int)key) != HotkeyModifiers.None;
}
