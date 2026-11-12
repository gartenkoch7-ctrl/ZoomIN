using Windows.Win32.UI.Input.KeyboardAndMouse;
using ZoomIN.Core.Tracking;
using ZoomIN.Platform.Input;

namespace ZoomIN.Platform.Tests.Input;

/// <summary>The hold key from key events and the real key state (E1), without a hook.</summary>
public sealed class HoldKeyTrackerTests
{
    [Fact]
    public void KeyHeldAtStart_IsNoMissedPress()
    {
        var tracker = new HoldKeyTracker();
        var handler = new CountingHandler();
        tracker.Start(MarkerHoldKey.RightShift, down: true);

        Assert.False(tracker.OnKeyState(handler, pressed: true));
        Assert.False(tracker.OnKeyState(handler, pressed: true));
        Assert.Equal(2, handler.Downs);
    }

    [Fact]
    public void MissedPress_NeedsTwoChecks_AndIsReportedOnce()
    {
        var tracker = new HoldKeyTracker();
        var handler = new CountingHandler();
        tracker.Start(MarkerHoldKey.RightShift, down: false);

        Assert.False(tracker.OnKeyState(handler, pressed: true)); // may be the lag right after a release
        Assert.Equal(0, handler.Downs);
        Assert.True(tracker.OnKeyState(handler, pressed: true));
        Assert.False(tracker.OnKeyState(handler, pressed: false)); // may be the lag right after a press
        Assert.Equal((1, 0), (handler.Downs, handler.Ups));
        Assert.False(tracker.OnKeyState(handler, pressed: false));
        Assert.Equal((1, 1), (handler.Downs, handler.Ups));

        tracker.OnKeyState(handler, pressed: true);
        Assert.False(tracker.OnKeyState(handler, pressed: true)); // reported only once per session
    }

    [Fact]
    public void KeyStateLaggingRightAfterAPress_DoesNotEndTheHold()
    {
        var tracker = new HoldKeyTracker();
        var handler = new CountingHandler();
        tracker.Start(MarkerHoldKey.RightShift, down: false);

        tracker.OnKey(handler, VIRTUAL_KEY.VK_RSHIFT, 0x36, down: true, up: false);
        tracker.OnKeyState(handler, pressed: false); // Windows has not updated the key state yet
        tracker.OnKeyState(handler, pressed: true);

        Assert.Equal(0, handler.Ups);
        Assert.Equal(2, handler.Downs);
    }

    [Fact]
    public void MissedRelease_EndsTheHold_EvenWhileTyping()
    {
        var tracker = new HoldKeyTracker();
        var handler = new CountingHandler();
        tracker.Start(MarkerHoldKey.RightShift, down: false);
        tracker.OnKey(handler, VIRTUAL_KEY.VK_RSHIFT, 0x36, down: true, up: false);

        // The release went to an elevated window; the user keeps typing between the checks.
        tracker.OnKeyState(handler, pressed: false);
        tracker.OnKey(handler, VIRTUAL_KEY.VK_A, 0x1E, down: true, up: false);
        tracker.OnKeyState(handler, pressed: false);

        Assert.Equal(1, handler.Ups);
    }

    [Fact]
    public void KeyEvents_PressReleaseAndTyping()
    {
        var tracker = new HoldKeyTracker();
        var handler = new CountingHandler();
        tracker.Start(MarkerHoldKey.RightShift, down: false);

        tracker.OnKey(handler, VIRTUAL_KEY.VK_RSHIFT, 0x36, down: true, up: false);
        tracker.OnKey(handler, VIRTUAL_KEY.VK_A, 0x1E, down: true, up: false);
        tracker.OnKey(handler, VIRTUAL_KEY.VK_RSHIFT, 0x36, down: false, up: true);

        Assert.Equal((1, 1, 1), (handler.Downs, handler.Others, handler.Ups));
    }

    private sealed class CountingHandler : IHoldKeyHandler
    {
        public int Downs { get; private set; }

        public int Ups { get; private set; }

        public int Others { get; private set; }

        public MarkerHoldKey Key => MarkerHoldKey.RightShift;

        public void HoldKeyDown() => Downs++;

        public void HoldKeyUp() => Ups++;

        public void OtherKeyDown() => Others++;
    }
}
