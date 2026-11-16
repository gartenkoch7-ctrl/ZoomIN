using Microsoft.Extensions.Logging.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Platform.Input;

namespace ZoomIN.Platform.Tests.Input;

/// <summary>
/// Real RegisterHotKey calls with a combination nobody types (Ctrl+Alt+Shift+F24), so they cannot
/// collide with the user's shortcuts. Thread-bound hotkeys: everything runs on the test thread.
/// </summary>
public sealed class HotkeyTableTests
{
    private const int ProbeId = 0x7F00;
    private static readonly HotkeyGesture s_gesture = new(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, 0x87);

    [Fact]
    public void OtherDesktopGetsTheInput_ShortcutIsFree_UntilItComesBack()
    {
        var table = new HotkeyTable(NullLogger.Instance);
        try
        {
            Assert.True(table.Add(s_gesture, () => { }, repeat: false));
            Assert.False(IsFree());

            table.DesktopChanged(ownDesktopHasInput: false);
            Assert.True(table.Suspended);
            Assert.True(IsFree());

            table.DesktopChanged(ownDesktopHasInput: true);
            Assert.False(table.Suspended);
            Assert.False(IsFree());
            Assert.False(table.NeedsRetry);
        }
        finally
        {
            table.Clear();
        }
    }

    [Fact]
    public void HeldElsewhere_IsRefused_ThenTakenOnRetry_OnceFree()
    {
        Assert.True(Hold());
        var table = new HotkeyTable(NullLogger.Instance);
        try
        {
            Assert.False(table.Add(s_gesture, () => { }, repeat: false));
            Assert.True(table.NeedsRetry);

            Release();
            table.Retry();

            Assert.False(table.NeedsRetry);
            Assert.False(IsFree());
        }
        finally
        {
            Release();
            table.Clear();
        }
    }

    [Fact]
    public void HeldForGood_GivesUpAfterTheLastRetry()
    {
        Assert.True(Hold());
        var table = new HotkeyTable(NullLogger.Instance);
        try
        {
            Assert.False(table.Add(s_gesture, () => { }, repeat: false));
            for (var i = 0; i < HotkeyTable.MaxRetries; i++)
            {
                table.Retry();
            }

            Assert.False(table.NeedsRetry);
        }
        finally
        {
            Release();
            table.Clear();
        }
    }

    [Fact]
    public void OwnShortcut_HeldAfterTheSwitchBack_IsNeverGivenUp()
    {
        var table = new HotkeyTable(NullLogger.Instance);
        try
        {
            Assert.True(table.Add(s_gesture, () => { }, repeat: false));
            table.DesktopChanged(ownDesktopHasInput: false);
            Assert.True(table.NeedsTimer);
            Assert.True(Hold()); // the secure instance lingers

            table.DesktopChanged(ownDesktopHasInput: true);
            for (var i = 0; i < HotkeyTable.MaxRetries + 5; i++)
            {
                table.Retry();
            }

            Assert.True(table.NeedsRetry);
            Release();
            table.Retry();
            Assert.False(table.NeedsTimer);
            Assert.False(IsFree());
        }
        finally
        {
            Release();
            table.Clear();
        }
    }

    [Fact]
    public void UnknownInputDesktop_ChangesNothing()
    {
        var table = new HotkeyTable(NullLogger.Instance);
        try
        {
            Assert.True(table.Add(s_gesture, () => { }, repeat: false));
            table.DesktopChanged(ownDesktopHasInput: null);
            Assert.False(table.Suspended);
            Assert.False(IsFree());

            table.DesktopChanged(ownDesktopHasInput: false);
            table.DesktopChanged(ownDesktopHasInput: null);
            Assert.True(table.Suspended);
        }
        finally
        {
            table.Clear();
        }
    }

    [Fact]
    public void AddedWhileSuspended_IsRegisteredWhenTheDesktopComesBack()
    {
        var table = new HotkeyTable(NullLogger.Instance);
        try
        {
            table.DesktopChanged(ownDesktopHasInput: false);
            Assert.True(table.Add(s_gesture, () => { }, repeat: false));
            Assert.True(IsFree());

            table.DesktopChanged(ownDesktopHasInput: true);
            Assert.False(IsFree());
        }
        finally
        {
            table.Clear();
        }
    }

    private static bool Hold() =>
        PInvoke.RegisterHotKey(HWND.Null, ProbeId, (HOT_KEY_MODIFIERS)s_gesture.Modifiers | HOT_KEY_MODIFIERS.MOD_NOREPEAT, (uint)s_gesture.VirtualKey);

    private static void Release() => PInvoke.UnregisterHotKey(HWND.Null, ProbeId);

    private static bool IsFree()
    {
        if (!Hold())
        {
            return false;
        }

        Release();
        return true;
    }
}
