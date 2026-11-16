using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Overview;
using ZoomIN.Platform.Input;

namespace ZoomIN.Platform.Tests.Input;

/// <summary>Release of the shortcut and Esc during the overview (B2), as the keyboard hook sees them.</summary>
public sealed class OverviewKeyWatchTests
{
    private const int O = 0x4F;
    private const int Escape = 0x1B;
    private const int LeftControl = 0xA2;
    private const int RightAlt = 0xA5;
    private const int LeftShift = 0xA0;
    private static readonly HotkeyGesture s_gesture = HotkeyGesture.Overview;

    [Fact]
    public void ReleaseOfTheMainKey_IsReportedOnce()
    {
        var (watch, handler, _) = Create();
        watch.Watch(7, s_gesture, handler);

        Assert.False(watch.OnKey(O, down: false, up: true, time: 100));
        watch.OnKey(LeftControl, down: false, up: true, time: 100);

        Assert.Equal(["released 7"], handler.Signals);
    }

    [Theory]
    [InlineData(LeftControl)]
    [InlineData(RightAlt)]
    public void ReleaseOfAModifier_CountsToo_ButNotOfOtherKeys(int modifier)
    {
        var (watch, handler, _) = Create();
        watch.Watch(1, s_gesture, handler);

        watch.OnKey(LeftShift, down: false, up: true, time: 100);
        watch.OnKey(0x41, down: false, up: true, time: 100);
        Assert.Empty(handler.Signals);

        watch.OnKey(modifier, down: false, up: true, time: 100);
        Assert.Equal(["released 1"], handler.Signals);
    }

    [Fact]
    public void ReleaseBeforeTheWatch_IsReportedAtOnce()
    {
        var (watch, handler, keys) = Create();
        keys.Remove(O);

        watch.Watch(3, s_gesture, handler);

        Assert.Equal(["released 3 late"], handler.Signals);
    }

    [Fact]
    public async Task ReleaseTheHookMissed_IsFoundByTheSecondLook()
    {
        var (watch, handler, keys) = Create();
        watch.Watch(4, s_gesture, handler);
        Assert.Empty(handler.Signals);

        lock (keys)
        {
            keys.Remove(O); // went up while the watch was being published
        }

        for (var i = 0; i < 100 && handler.Signals.Count == 0; i++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.Equal(["released 4 late"], handler.Signals);
    }

    [Fact]
    public void Escape_IsSwallowed_OnlyWhileWatching_WithRepeatsAndRelease()
    {
        var (watch, handler, _) = Create();
        Assert.False(watch.OnKey(Escape, down: true, up: false, time: 100));
        Assert.False(watch.OnKey(Escape, down: false, up: true, time: 100));

        watch.Watch(5, s_gesture, handler);
        Assert.True(watch.OnKey(Escape, down: true, up: false, time: 100));
        watch.Stop(5); // the overview ends while Esc is still down
        Assert.True(watch.OnKey(Escape, down: true, up: false, time: 100)); // key repeat
        Assert.True(watch.OnKey(Escape, down: false, up: true, time: 100));

        Assert.False(watch.OnKey(Escape, down: true, up: false, time: 100));
        Assert.Equal(["escape 5"], handler.Signals);
    }

    [Fact]
    public void MissedEscapeRelease_DoesNotSwallowALaterPress()
    {
        var (watch, handler, _) = Create();
        watch.Watch(1, s_gesture, handler);
        Assert.True(watch.OnKey(Escape, down: true, up: false, time: 1_000));
        watch.Stop(1);

        // No release arrived; a repeat 500 ms later still belongs to the press, one 5 s later does not.
        Assert.True(watch.OnKey(Escape, down: true, up: false, time: 1_500));
        Assert.False(watch.OnKey(Escape, down: true, up: false, time: 6_500));
        Assert.False(watch.OnKey(Escape, down: false, up: true, time: 6_600));
    }

    [Fact]
    public void EscapeHeldBeforeTheOverview_StaysWithTheApplication()
    {
        var (watch, handler, keys) = Create();
        keys.Add(Escape);
        watch.Watch(1, s_gesture, handler);

        Assert.False(watch.OnKey(Escape, down: true, up: false, time: 100)); // repeat
        Assert.False(watch.OnKey(Escape, down: false, up: true, time: 150));
        Assert.True(watch.OnKey(Escape, down: true, up: false, time: 900)); // a new press
        Assert.Equal(["escape 1"], handler.Signals);
    }

    [Fact]
    public void Stop_OfAnOlderWatch_LeavesTheCurrentOne()
    {
        var (watch, handler, _) = Create();
        watch.Watch(1, s_gesture, handler);
        watch.Watch(2, s_gesture, handler);

        watch.Stop(1);
        Assert.True(watch.OnKey(Escape, down: true, up: false, time: 100));
        watch.OnKey(Escape, down: false, up: true, time: 100);

        watch.Stop(2);
        Assert.False(watch.OnKey(Escape, down: true, up: false, time: 100));
        Assert.Equal(["escape 2"], handler.Signals);
    }

    [Fact]
    public void ModifierOf_MapsLeftAndRightKeys()
    {
        Assert.Equal(HotkeyModifiers.Control, KeyState.ModifierOf(LeftControl));
        Assert.Equal(HotkeyModifiers.Alt, KeyState.ModifierOf(RightAlt));
        Assert.Equal(HotkeyModifiers.Shift, KeyState.ModifierOf(LeftShift));
        Assert.Equal(HotkeyModifiers.Windows, KeyState.ModifierOf(0x5B));
        Assert.Equal(HotkeyModifiers.None, KeyState.ModifierOf(O));
    }

    internal static (OverviewKeyWatch Watch, Recorder Handler, HashSet<int> Down) Create()
    {
        // The shortcut is held when the watch starts, unless a test lets go of it.
        var down = new HashSet<int> { O, KeyState.Control, KeyState.Alt };
        bool IsDown(int key)
        {
            lock (down)
            {
                return down.Contains(key);
            }
        }

        return (new OverviewKeyWatch(NullLogger.Instance, IsDown, TimeProvider.System), new Recorder(), down);
    }

    internal sealed class Recorder : IOverviewKeyHandler
    {
        public List<string> Signals { get; } = [];

        public void ShortcutReleased(long watchId, bool late) => Signals.Add($"released {watchId}{(late ? " late" : string.Empty)}");

        public void EscapePressed(long watchId) => Signals.Add($"escape {watchId}");
    }
}
