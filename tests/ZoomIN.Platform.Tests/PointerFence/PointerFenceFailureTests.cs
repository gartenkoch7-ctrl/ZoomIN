using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;
using ZoomIN.Platform.PointerFence;

namespace ZoomIN.Platform.Tests.PointerFence;

/// <summary>ADR-0028: every failure leaves the pointer free and never throws into a shortcut handler.</summary>
public sealed class PointerFenceFailureTests
{
    private readonly FakeFenceNative _native = new();
    private readonly FakeMonitors _monitors = new();

    [Fact]
    public void ClipCursorThrows_ReleasesAndStaysOff()
    {
        using var fence = Create();
        _native.ClipThrows = true;

        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Right]);

        Assert.Equal(FenceMode.Off, fence.Mode);
        Assert.False(_native.Confined);
        Assert.Equal(1, _native.Unclips);
    }

    [Fact]
    public void ClipCursorRefused_KeepsTrying_OnTheCheck()
    {
        using var fence = Create();
        _native.ClipSucceeds = false;
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Right]);
        Assert.False(_native.Confined);

        _native.ClipSucceeds = true;
        ((IFenceEvents)fence).OnCheck();

        Assert.Equal(new PhysicalRect(0, 0, 3840, 1080), _native.Clip);
    }

    [Fact]
    public void HookRefused_ReleasesAndStaysOff()
    {
        using var fence = Create();
        _native.HookRefused = true;

        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Middle]);

        Assert.Equal(FenceMode.Off, fence.Mode);
        Assert.False(_native.Confined);
        Assert.Null(_native.Hook);
    }

    [Fact]
    public void HookThrows_ReleasesAClipHeldBefore()
    {
        using var fence = Create();
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Right]);
        _native.HookThrows = true;

        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Middle]);

        Assert.Equal(FenceMode.Off, fence.Mode);
        Assert.False(_native.Confined);

        // The owner still freezes: the watcher stays, so a later desktop switch tries again.
        Assert.True(_native.WatcherActive);
        _native.HookThrows = false;
        _native.Events!.OnTrigger(FenceTrigger.DesktopSwitch);
        Assert.Equal(FenceMode.Hook, fence.Mode);
    }

    [Fact]
    public void HookDisposeThrows_StillReleasesTheClipAndTheWatcher()
    {
        using var fence = Create();
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Middle]);
        _native.Hook!.ThrowOnDispose = true;
        var watcher = _native.Watcher!;

        fence.Forbid(FenceOwner.FrozenMonitors, []);

        Assert.Equal(FenceMode.Off, fence.Mode);
        Assert.True(watcher.Disposed);
        Assert.False(_native.Confined);
    }

    [Fact]
    public void SilentHook_RefusedOnReinstall_ReleasesTheFence()
    {
        using var fence = Create();
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Middle]);
        ((IFenceEvents)fence).OnCheck();
        _native.HookRefused = true;

        _native.Cursor = new PhysicalPoint(700, 700);
        ((IFenceEvents)fence).OnCheck();
        _native.Cursor = new PhysicalPoint(800, 700);
        ((IFenceEvents)fence).OnCheck();

        Assert.Equal(FenceMode.Off, fence.Mode);
        Assert.False(_native.HookActive);
    }

    [Fact]
    public void HookRefused_IsRetriedByTheCheck_UntilItWorks()
    {
        using var fence = Create();
        _native.HookRefused = true;
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Middle]);
        Assert.Equal(FenceMode.Off, fence.Mode);

        ((IFenceEvents)fence).OnCheck();
        Assert.Equal(FenceMode.Off, fence.Mode);

        _native.HookRefused = false;
        ((IFenceEvents)fence).OnCheck();

        Assert.Equal(FenceMode.Hook, fence.Mode);
        Assert.True(_native.HookActive);
    }

    [Fact]
    public void HookRefused_WithOneRectangle_FallsBackToTheClip()
    {
        using var fence = Create();
        _native.HookRefused = true;
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Right]);

        fence.AddFilter(new Nudge());

        Assert.Equal(FenceMode.Clip, fence.Mode);
        Assert.Equal(new PhysicalRect(0, 0, 3840, 1080), _native.Clip);
    }

    [Fact]
    public void WatcherUnavailable_FenceStillWorks()
    {
        using var fence = Create();
        _native.WatcherThrows = true;

        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Right]);

        Assert.Equal(FenceMode.Clip, fence.Mode);
        fence.Dispose();
        Assert.False(_native.Confined);
    }

    [Fact]
    public void MonitorProviderThrows_ReleasesEverything()
    {
        using var fence = Create();
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Right]);
        _monitors.Throws = true;

        _native.Events!.OnTrigger(FenceTrigger.DisplayChange);

        Assert.Equal(FenceMode.Off, fence.Mode);
        Assert.False(_native.Confined);
    }

    [Fact]
    public void RecomputedHandlerThrows_DoesNotEscapeTheWatcherThread()
    {
        using var fence = Create();
        fence.Recomputed += (_, _) => throw new InvalidOperationException("owner failed");
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Right]);

        _native.Events!.OnTrigger(FenceTrigger.SessionUnlock);

        Assert.Equal(FenceMode.Clip, fence.Mode);
    }

    [Fact]
    public void ForbidNull_Throws_WithoutChangingTheFence()
    {
        using var fence = Create();

        Assert.Throws<ArgumentNullException>(() => fence.Forbid(FenceOwner.FrozenMonitors, null!));
        Assert.Throws<ArgumentNullException>(() => fence.AddFilter(null!));
        Assert.Equal(FenceMode.Off, fence.Mode);
    }

    private Win32PointerFence Create() => new(NullLogger.Instance, _monitors, _native, TimeProvider.System);

    private sealed class Nudge : IPointerMoveFilter
    {
        public int Order => PointerFilterOrder.AxisLock;

        public PhysicalPoint Filter(long timestamp, PhysicalPoint current, PhysicalPoint requested) => requested with { Y = current.Y };
    }
}
