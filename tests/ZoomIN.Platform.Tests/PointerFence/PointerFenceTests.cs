using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;
using ZoomIN.Platform.PointerFence;

namespace ZoomIN.Platform.Tests.PointerFence;

/// <summary>The fence's modes and re-applying, against a fake Windows (no real ClipCursor or hook).</summary>
public sealed class PointerFenceTests
{
    private readonly FakeFenceNative _native = new();
    private readonly FakeMonitors _monitors = new();

    [Fact]
    public void Idle_RunsNothing()
    {
        using var fence = Create();

        Assert.Equal(FenceMode.Off, fence.Mode);
        Assert.Null(_native.Hook);
        Assert.Null(_native.Watcher);
        Assert.Equal(0, _native.ClipCalls);
    }

    [Fact]
    public void ForbiddenEdgeMonitor_ClipsToTheRest_AndWatches()
    {
        using var fence = Create();

        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Right]);

        Assert.Equal(FenceMode.Clip, fence.Mode);
        Assert.Equal(new PhysicalRect(0, 0, 3840, 1080), _native.Clip);
        Assert.False(_native.HookActive);
        Assert.True(_native.WatcherActive);
    }

    [Fact]
    public void ForbiddenMiddleMonitor_UsesTheHook_AndNoClip()
    {
        using var fence = Create();

        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Middle]);

        Assert.Equal(FenceMode.Hook, fence.Mode);
        Assert.True(_native.HookActive);
        Assert.False(_native.Confined);
        Assert.True(_native.Target!.OnMove(new PhysicalPoint(1910, 5), new PhysicalPoint(1950, 5), out var allowed));
        Assert.Equal(new PhysicalPoint(1919, 5), allowed);
        Assert.False(_native.Target.OnMove(new PhysicalPoint(10, 5), new PhysicalPoint(20, 5), out _));
    }

    [Fact]
    public void SwitchingFromClipToHook_ReleasesTheClip()
    {
        using var fence = Create();
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Right]);

        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Middle]);

        Assert.Equal(FenceMode.Hook, fence.Mode);
        Assert.False(_native.Confined);
        Assert.True(_native.HookActive);
    }

    [Fact]
    public void ClearingAllAreas_ReleasesEverything()
    {
        using var fence = Create();
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Middle]);

        fence.Forbid(FenceOwner.FrozenMonitors, []);

        Assert.Equal(FenceMode.Off, fence.Mode);
        Assert.False(_native.HookActive);
        Assert.False(_native.WatcherActive);
        Assert.False(_native.Confined);
    }

    [Fact]
    public void Owners_AddUp_AndAreReplacedSeparately()
    {
        using var fence = Create();
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Right]);
        fence.Forbid(FenceOwner.IslandEdit, [new PhysicalRect(0, 0, 3840, 40)]);

        Assert.Equal(new PhysicalRect(0, 40, 3840, 1040), _native.Clip);

        fence.Forbid(FenceOwner.IslandEdit, []);

        Assert.Equal(new PhysicalRect(0, 0, 3840, 1080), _native.Clip);
    }

    [Fact]
    public void EverythingForbidden_StaysOff_NeverTraps()
    {
        using var fence = Create();

        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Left, FakeMonitors.Middle, FakeMonitors.Right]);

        Assert.Equal(FenceMode.Off, fence.Mode);
        Assert.False(_native.Confined);
        Assert.Null(_native.Hook);
    }

    [Fact]
    public void Filter_KeepsTheHook_UntilRemoved()
    {
        using var fence = Create();
        var filter = new StopAll();

        fence.AddFilter(filter);
        fence.AddFilter(filter);

        Assert.Equal(FenceMode.Hook, fence.Mode);
        Assert.True(_native.Target!.OnMove(new PhysicalPoint(1, 1), new PhysicalPoint(9, 9), out var allowed));
        Assert.Equal(new PhysicalPoint(1, 1), allowed);

        fence.RemoveFilter(filter);

        Assert.Equal(FenceMode.Off, fence.Mode);
        Assert.False(_native.HookActive);
    }

    [Fact]
    public void DesktopSwitch_ReappliesTheClip_AndTellsTheOwners()
    {
        using var fence = Create();
        var triggers = new List<FenceTrigger>();
        fence.Recomputed += (_, trigger) => triggers.Add(trigger);
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Right]);
        _native.Clip = null; // Windows resets ClipCursor on Win+L

        _native.Events!.OnTrigger(FenceTrigger.DesktopSwitch);

        Assert.Equal(new PhysicalRect(0, 0, 3840, 1080), _native.Clip);
        Assert.Equal([FenceTrigger.DesktopSwitch], triggers);
    }

    [Fact]
    public void DisplayChange_RecomputesAgainstTheNewMonitors()
    {
        using var fence = Create();
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Right]);

        // The frozen monitor was unplugged: its rectangle no longer overlaps anything.
        _monitors.Bounds.Remove(@"\\.\DISPLAY2");
        _native.Events!.OnTrigger(FenceTrigger.DisplayChange);

        Assert.Equal(FenceMode.Off, fence.Mode);
        Assert.False(_native.Confined);
    }

    [Fact]
    public void DisplayChange_LastFreeMonitorGone_ReleasesEverything()
    {
        using var fence = Create();
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Middle, FakeMonitors.Right]);
        Assert.Equal(FenceMode.Clip, fence.Mode);

        _monitors.Bounds.Remove(@"\\.\DISPLAY1");
        _native.Events!.OnTrigger(FenceTrigger.DisplayChange);

        Assert.Equal(FenceMode.Off, fence.Mode);
        Assert.False(_native.Confined);

        // The owner still freezes: the watcher stays, and the next change brings the fence back.
        Assert.True(_native.WatcherActive);
        _monitors.Bounds[@"\\.\DISPLAY1"] = FakeMonitors.Left;
        _native.Events!.OnTrigger(FenceTrigger.DisplayChange);
        Assert.Equal(new PhysicalRect(0, 0, 1920, 1080), _native.Clip);
    }

    [Fact]
    public void Dispose_ReleasesClipHookAndWatcher_AndIgnoresLaterRequests()
    {
        var fence = Create();
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Right]);
        var watcher = _native.Watcher!;

        fence.Dispose();
        fence.Dispose();
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Right]);
        ((IFenceEvents)fence).OnTrigger(FenceTrigger.DesktopSwitch);
        ((IFenceEvents)fence).OnCheck();

        Assert.False(_native.Confined);
        Assert.True(watcher.Disposed);
        Assert.Equal(FenceMode.Off, fence.Mode);
    }

    [Fact]
    public void ProcessExit_ReleasesTheClip()
    {
        using var fence = Create();
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Right]);

        fence.ReleaseForExit();

        Assert.False(_native.Confined);
    }

    [Fact]
    public void ReleasingWithoutOwnClip_LeavesOtherProgramsAlone()
    {
        using var fence = Create();
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Middle]);

        fence.Forbid(FenceOwner.FrozenMonitors, []);
        fence.ReleaseForExit();

        Assert.Equal(0, _native.Unclips);
    }

    private Win32PointerFence Create() => new(NullLogger.Instance, _monitors, _native, TimeProvider.System);

    private sealed class StopAll : IPointerMoveFilter
    {
        public int Order => PointerFilterOrder.AxisLock;

        public PhysicalPoint Filter(long timestamp, PhysicalPoint current, PhysicalPoint requested) => current;
    }
}
