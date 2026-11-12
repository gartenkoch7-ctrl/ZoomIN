using Windows.Win32.UI.Input.KeyboardAndMouse;
using ZoomIN.Core.Tracking;

namespace ZoomIN.Platform.Input;

/// <summary>
/// Virtual keys of the hold key (E1). Low-level keyboard hooks report the side-specific codes
/// (VK_LSHIFT, VK_RSHIFT, …), never the generic VK_SHIFT, so left and right are told apart.
/// </summary>
internal static class HoldKeyCodes
{
    public static VIRTUAL_KEY For(MarkerHoldKey key) => key switch
    {
        MarkerHoldKey.LeftShift => VIRTUAL_KEY.VK_LSHIFT,
        MarkerHoldKey.RightCtrl => VIRTUAL_KEY.VK_RCONTROL,
        MarkerHoldKey.LeftCtrl => VIRTUAL_KEY.VK_LCONTROL,
        MarkerHoldKey.RightAlt => VIRTUAL_KEY.VK_RMENU,
        MarkerHoldKey.LeftAlt => VIRTUAL_KEY.VK_LMENU,
        _ => VIRTUAL_KEY.VK_RSHIFT,
    };

    // Alt Gr sends a left Ctrl with this scan code along with right Alt; no one pressed left Ctrl.
    private const uint AltGrControlScanCode = 0x21D;

    /// <summary>What one key event means for the hold key.</summary>
    public static HoldKeyEvent Classify(VIRTUAL_KEY key, uint scanCode, bool down, bool up, MarkerHoldKey holdKey)
    {
        if (key == VIRTUAL_KEY.VK_LCONTROL && scanCode == AltGrControlScanCode)
        {
            return HoldKeyEvent.None;
        }

        if (key == For(holdKey))
        {
            return down ? HoldKeyEvent.Down : up ? HoldKeyEvent.Up : HoldKeyEvent.None;
        }

        // Only a real key counts as typing. Modifiers do not: Alt Gr sends a left Ctrl along with
        // right Alt (also on key repeat), which would otherwise cancel holding right Alt.
        return down && !ModifierKeys.Is(key) ? HoldKeyEvent.Other : HoldKeyEvent.None;
    }
}
