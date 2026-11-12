using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;
using ZoomIN.Platform.PointerFence;

namespace ZoomIN.Platform.Tests.PointerFence;

/// <summary>The periodic check: dropped clips come back, foreign clips stay, a removed hook is installed again.</summary>
public sealed class PointerFenceRecoveryTests
{
    private readonly FakeFenceNative _native = new();
    private readonly FakeMonitors _monitors = new();

    [Fact]
    public void ForeignClipAtApply_IsLeftAlone_AndNotReleased()
    {
        using var fence = Create();
        var game = new PhysicalRect(100, 100, 800, 600);
        _native.Clip = game;

        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Right]);
        fence.Forbid(FenceOwner.FrozenMonitors, []);

        Assert.Equal(game, _native.Clip);
        Assert.Equal(0, _native.Unclips);
    }

    [Fact]
    public void SilentHook_IsInstalledAgain()
    {
        using var fence = Create();
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Middle]);
        var first = _native.Hook!;
        ((IFenceEvents)fence).OnCheck();

        // The pointer moved, the hook saw nothing: once could be a SetCursorPos, twice in a row it is gone.
        _native.Cursor = new PhysicalPoint(500, 500);
        ((IFenceEvents)fence).OnCheck();
        Assert.False(first.Disposed);

        _native.Cursor = new PhysicalPoint(600, 500);
        ((IFenceEvents)fence).OnCheck();

        Assert.True(first.Disposed);
        Assert.True(_native.HookActive);
        Assert.Equal(FenceMode.Hook, fence.Mode);
    }

    [Fact]
    public void ClipCutByWindows_StaysOurs_AndIsReleased()
    {
        using var fence = Create();
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Right]);

        // A display change shrank the virtual screen, and Windows cut our clip to it, before
        // WM_DISPLAYCHANGE arrived: still our clip, not a game's.
        _native.Screen = new PhysicalRect(0, 0, 2000, 1080);
        
        _native.Clip = new PhysicalRect(0, 0, 2000, 1080);
        ((IFenceEvents)fence).OnCheck();
        fence.Forbid(FenceOwner.FrozenMonitors, []);

        Assert.Null(_native.Clip);
        Assert.Equal(1, _native.Unclips);
    }

    [Fact]
    public void AfterProcessExit_TheCheckNeverClipsAgain()
    {
        using var fence = Create();
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Right]);

        fence.ReleaseForExit();
        ((IFenceEvents)fence).OnCheck();
        _native.Events!.OnTrigger(FenceTrigger.DesktopSwitch);

        Assert.False(_native.Confined);
    }

    [Fact]
    public void FilterWithOneRectangle_UsesHookAndClip()
    {
        using var fence = Create();
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Right]);

        fence.AddFilter(new PassAll());

        Assert.Equal(FenceMode.Hook, fence.Mode);
        Assert.True(_native.HookActive);
        Assert.Equal(new PhysicalRect(0, 0, 3840, 1080), _native.Clip);
    }

    [Fact]
    public void InputTheHookNeverSees_ReinstallsOnlyOnce()
    {
        using var fence = Create();
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Middle]);
        ((IFenceEvents)fence).OnCheck();

        // E.g. an elevated window in front: the pointer moves, the hook is never called.
        var hooks = new List<FakeFenceNative.FakeHandle>();
        for (var i = 1; i <= 8; i++)
        {
            _native.Cursor = new PhysicalPoint(100 * i, 300);
            ((IFenceEvents)fence).OnCheck();
            if (!hooks.Contains(_native.Hook!))
            {
                hooks.Add(_native.Hook!);
            }
        }

        Assert.Equal(2, hooks.Count);
    }

    [Fact]
    public void LiveHook_IsKept()
    {
        using var fence = Create();
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Middle]);
        var first = _native.Hook!;
        ((IFenceEvents)fence).OnCheck();

        for (var i = 1; i <= 3; i++)
        {
            _native.Cursor = new PhysicalPoint(100 * i, 500);
            _native.Target!.OnMove(new PhysicalPoint(10, 10), new PhysicalPoint(100 * i, 500), out _);
            ((IFenceEvents)fence).OnCheck();
        }

        Assert.False(first.Disposed);
    }

    [Fact]
    public void Check_ReappliesAClipWindowsDropped()
    {
        using var fence = Create();
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Right]);
        _native.Clip = null;

        ((IFenceEvents)fence).OnCheck();

        Assert.Equal(new PhysicalRect(0, 0, 3840, 1080), _native.Clip);
    }

    [Fact]
    public void Check_LeavesAForeignClip_AndDoesNotReleaseItLater()
    {
        using var fence = Create();
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Right]);
        var game = new PhysicalRect(3000, 100, 1600, 600);
        _native.Clip = game;

        ((IFenceEvents)fence).OnCheck();
        fence.Forbid(FenceOwner.FrozenMonitors, []);

        Assert.Equal(game, _native.Clip);
    }

    [Fact]
    public void Check_WithoutClip_DoesNothing()
    {
        using var fence = Create();
        fence.Forbid(FenceOwner.FrozenMonitors, [FakeMonitors.Middle]);
        var calls = _native.ClipCalls;

        ((IFenceEvents)fence).OnCheck();

        Assert.Equal(calls, _native.ClipCalls);
    }

    private Win32PointerFence Create() => new(NullLogger.Instance, _monitors, _native, TimeProvider.System);

    private sealed class PassAll : IPointerMoveFilter
    {
        public int Order => PointerFilterOrder.MouseBuffer;

        public PhysicalPoint Filter(long timestamp, PhysicalPoint current, PhysicalPoint requested) => requested;
    }
}
