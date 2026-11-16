using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;
using ZoomIN.Platform.PointerFence;

namespace ZoomIN.Platform.Tests.PointerFence;

/// <summary>
/// The real Windows calls, kept harmless: the clip is the whole virtual screen (confines nothing)
/// and is released in finally; the hook lets every move through and is removed at once.
/// </summary>
public sealed class Win32FenceNativeTests
{
    [Fact]
    public void ClipToTheWholeScreen_IsReadBack_AndReleased()
    {
        var native = new Win32FenceNative();
        var screen = native.VirtualScreen();

        // Another program (game, remote tool) confines the pointer right now: leave its clip alone.
        Assert.SkipWhen(native.CurrentClip() != screen, "Another program confines the pointer.");
        try
        {
            // Fails without an interactive desktop (CI service session); then there is nothing to check.
            if (native.ClipTo(screen, out _))
            {
                Assert.Equal(screen, native.CurrentClip());
            }
        }
        finally
        {
            native.Unclip();
        }

        Assert.True(screen.Width > 0 && screen.Height > 0);
    }

    [Fact]
    public void MoveHook_InstallsAndUninstalls()
    {
        var native = new Win32FenceNative();
        var hook = native.InstallMoveHook(new PassThrough());
        try
        {
            Assert.NotNull(hook);
        }
        finally
        {
            hook?.Dispose();
        }
    }

    [Fact]
    public void Watcher_StartsAndStops_TwiceInARow()
    {
        var native = new Win32FenceNative();
        var events = new CountingEvents();

        // The window class is registered once; a second watcher must reuse it.
        native.StartWatcher(events).Dispose();
        native.StartWatcher(events).Dispose();
    }

    private sealed class PassThrough : IFenceHookTarget
    {
        public bool OnMove(PhysicalPoint current, PhysicalPoint requested, out PhysicalPoint allowed)
        {
            allowed = requested;
            return false;
        }
    }

    private sealed class CountingEvents : IFenceEvents
    {
        public int Triggers { get; private set; }

        public void OnTrigger(FenceTrigger trigger) => Triggers++;

        public void OnCheck()
        {
        }
    }
}
