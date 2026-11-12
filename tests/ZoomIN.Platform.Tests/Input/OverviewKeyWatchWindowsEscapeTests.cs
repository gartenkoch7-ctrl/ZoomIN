using ZoomIN.Core.Hotkeys;
using ZoomIN.Platform.Input;

namespace ZoomIN.Platform.Tests.Input;

/// <summary>Which Esc during the overview (B2) belongs to Windows and which ends the overview.</summary>
public sealed class OverviewKeyWatchWindowsEscapeTests
{
    private const int O = 0x4F;
    private const int Escape = 0x1B;
    private static readonly HotkeyGesture s_gesture = HotkeyGesture.Overview;

    [Fact]
    public void CtrlShiftEscape_ReachesWindows_ButCtrlAltEscape_EndsTheOverview()
    {
        var (watch, handler, keys) = OverviewKeyWatchTests.Create();
        watch.Watch(1, s_gesture, handler);
        Hold(keys, KeyState.Control, KeyState.Shift); // tapped; Task Manager

        Assert.False(watch.OnKey(Escape, down: true, up: false, time: 100));
        Assert.False(watch.OnKey(Escape, down: false, up: true, time: 150));
        Assert.Empty(handler.Signals);

        Hold(keys, KeyState.Control, KeyState.Alt); // the shortcut's own modifiers, still held

        Assert.True(watch.OnKey(Escape, down: true, up: false, time: 300));
        Assert.Equal(["escape 1"], handler.Signals);
    }

    [Fact]
    public void EscapePassedToWindows_KeepsItsRepeatsAndRelease()
    {
        var (watch, handler, keys) = OverviewKeyWatchTests.Create();
        watch.Watch(1, s_gesture, handler);
        Hold(keys, KeyState.Control, KeyState.Shift);

        Assert.False(watch.OnKey(Escape, down: true, up: false, time: 100));
        Hold(keys); // Ctrl and Shift let go, Esc still held

        Assert.False(watch.OnKey(Escape, down: true, up: false, time: 140));
        Assert.False(watch.OnKey(Escape, down: false, up: true, time: 180));
        Assert.Empty(handler.Signals);
    }

    [Fact]
    public void EscapeWhileHoldingAWindowsKeyShortcut_EndsTheOverview()
    {
        var (watch, handler, keys) = OverviewKeyWatchTests.Create();
        Hold(keys, O, KeyState.LeftWindows, KeyState.Alt);

        watch.Watch(2, new HotkeyGesture(HotkeyModifiers.Windows | HotkeyModifiers.Alt, O), handler);

        Assert.True(watch.OnKey(Escape, down: true, up: false, time: 100));
        Assert.Equal(["escape 2"], handler.Signals);
    }

    [Theory]
    [InlineData(KeyState.Control, true)] // left over from the shortcut (Ctrl+Esc waits for the end)
    [InlineData(KeyState.Alt, true)]
    [InlineData(KeyState.Shift, true)]
    [InlineData(KeyState.LeftWindows, false)] // Win+Esc
    public void EscapeWithOneModifier_AfterATap(int modifier, bool swallowed)
    {
        var (watch, handler, keys) = OverviewKeyWatchTests.Create();
        watch.Watch(1, s_gesture, handler);
        Hold(keys, modifier);

        Assert.Equal(swallowed, watch.OnKey(Escape, down: true, up: false, time: 100));
    }

    [Fact]
    public void Release_AfterAnEscape_IsNotReported()
    {
        var (watch, handler, _) = OverviewKeyWatchTests.Create();
        watch.Watch(3, s_gesture, handler);

        watch.OnKey(Escape, down: true, up: false, time: 100);
        watch.OnKey(O, down: false, up: true, time: 120);

        Assert.Equal(["escape 3"], handler.Signals);
    }

    private static void Hold(HashSet<int> keys, params int[] held)
    {
        lock (keys)
        {
            keys.Clear();
            keys.UnionWith(held);
        }
    }
}
