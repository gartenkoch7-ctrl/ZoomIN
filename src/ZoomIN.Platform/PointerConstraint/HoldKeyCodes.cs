using Windows.Win32.UI.Input.KeyboardAndMouse;
using ZoomIN.Core.Pointer;

namespace ZoomIN.Platform.PointerConstraint;

/// <summary>Key codes of the axis lock's hold key (A6): left and right variants as the low-level hook reports them.</summary>
internal static class HoldKeyCodes
{
    /// <summary>AltGr sends a fake left Ctrl with this scan code before the right Alt.</summary>
    public const uint AltGrFakeControlScanCode = 0x21D;

    // Keys Windows makes up: AltGr's left Ctrl carries this bit (0x21D); the Shift up/down it sends
    // around numpad and navigation keys while NumLock is on is reported either with this bit
    // (0x22A, 0x236) or as an extended (E0) Shift, which a real Shift key never is.
    private const uint FakeScanCodeBit = 0x200;
    private const uint LeftShiftScanCode = 0x2A;
    private const uint RightShiftScanCode = 0x36;

    /// <summary>A key event Windows generated itself, not a key the user pressed or released.</summary>
    public static bool IsFake(uint scanCode, bool extended) =>
        (scanCode & FakeScanCodeBit) != 0
        || (extended && (scanCode & 0xFF) is LeftShiftScanCode or RightShiftScanCode);

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
