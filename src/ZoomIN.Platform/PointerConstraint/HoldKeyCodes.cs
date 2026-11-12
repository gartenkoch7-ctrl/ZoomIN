using Windows.Win32.UI.Input.KeyboardAndMouse;
using ZoomIN.Core.Pointer;

namespace ZoomIN.Platform.PointerConstraint;

/// <summary>Key codes of the axis lock's hold key (A6): left and right variants as the low-level hook reports them.</summary>
internal static class HoldKeyCodes
{
    /// <summary>AltGr sends a fake left Ctrl with this scan code before the right Alt: not a held Ctrl.</summary>
    public const uint AltGrFakeControlScanCode = 0x21D;

    public static int VirtualKey(AxisLockKey key) => (int)(key switch
    {
        AxisLockKey.RightShift => VIRTUAL_KEY.VK_RSHIFT,
        AxisLockKey.LeftControl => VIRTUAL_KEY.VK_LCONTROL,
        AxisLockKey.RightControl => VIRTUAL_KEY.VK_RCONTROL,
        AxisLockKey.LeftAlt => VIRTUAL_KEY.VK_LMENU,
        AxisLockKey.RightAlt => VIRTUAL_KEY.VK_RMENU,
        _ => VIRTUAL_KEY.VK_LSHIFT,
    });
}
