using ZoomIN.App.Freeze;
using ZoomIN.App.Tests.Overview;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Viewport;
using static ZoomIN.App.Tests.Freeze.ThreeMonitors;

namespace ZoomIN.App.Tests.Freeze;

/// <summary>Ctrl+Alt+H and Ctrl+Alt+N with three monitors in the grilling layout 1 · 3 · 2 (G2 acceptance criteria).</summary>
public sealed class MonitorFreezeControllerTests
{
    [Fact]
    public void GrillingDemo_FreezeTwo_PointerToThree_ThenOneAndBack_ThenUnfreeze()
    {
        using var rig = new FreezeRig();
        rig.Models.Zoom.Activate(ZoomMode.Fullscreen, Two, [One, Two, Three]);
        rig.PointerTo(Two);

        rig.Controller.ToggleFreeze();

        var frozen = rig.Frozen.For(Two);
        Assert.NotNull(frozen);
        Assert.Equal(rig.Models.Zoom.TargetFor(Two), frozen.Level);
        Assert.Equal(Three, rig.PointerMonitor);
        Assert.Equal(PointerMoveReason.FreezeMonitor, rig.Mover.Moves.Single().Reason);
        Assert.Equal([new PhysicalRect(3840, 0, 1920, 1080)], rig.Fence.Forbidden);
        Assert.Contains("Monitor 2 frozen", rig.Announcer.Messages);

        // Ctrl+Alt+N: the right is frozen, so 3 → 1, then back to 3; never 2.
        rig.Switch.NextMonitor();
        Assert.Equal(One, rig.PointerMonitor);
        rig.Switch.NextMonitor();
        Assert.Equal(Three, rig.PointerMonitor);
        rig.Switch.NextMonitor();
        Assert.Equal(One, rig.PointerMonitor);
        Assert.All(rig.Mover.Moves.Skip(1), move => Assert.Equal(PointerMoveReason.NextMonitor, move.Reason));

        rig.Controller.ToggleFreeze();
        Assert.False(rig.Frozen.Any);
        Assert.Empty(rig.Fence.Forbidden);
        Assert.Contains("Monitors unfrozen", rig.Announcer.Messages);
        Assert.Equal(4, rig.Mover.Moves.Count); // unfreezing does not move the pointer
    }

    [Fact]
    public void OneMonitor_FreezeAndSwitch_DoNothing_ButGiveAHint()
    {
        var monitors = new ThreeMonitors();
        monitors.KeepOnly(One);
        using var rig = new FreezeRig(monitors);

        rig.Controller.ToggleFreeze();
        rig.Switch.NextMonitor();

        Assert.False(rig.Frozen.Any);
        Assert.Empty(rig.Mover.Moves);
        Assert.Empty(rig.Fence.Requests);
        Assert.Equal(["Freezing needs at least two monitors.", "There is no other monitor."], rig.Hints);
    }

    [Fact]
    public void RefusedMove_FreezesNothing()
    {
        using var rig = new FreezeRig();
        rig.PointerTo(Two);
        rig.Mover.Refuse = true;

        rig.Controller.ToggleFreeze();
        rig.Switch.NextMonitor();

        Assert.False(rig.Frozen.Any);
        Assert.Empty(rig.Fence.Requests);
        Assert.Equal(2, rig.Hints.Count);
    }

    [Fact]
    public void DesktopSwitch_AndUnlock_LiftEveryFreeze()
    {
        foreach (var trigger in new[] { FenceTrigger.DesktopSwitch, FenceTrigger.SessionUnlock })
        {
            using var rig = new FreezeRig();
            rig.PointerTo(One);
            rig.Controller.ToggleFreeze();

            rig.Fence.Raise(FenceTrigger.ClipLost);
            Assert.True(rig.Frozen.IsFrozen(One));

            rig.Fence.Raise(trigger);

            Assert.False(rig.Frozen.Any);
            Assert.Empty(rig.Fence.Forbidden);
        }
    }

    [Fact]
    public void FenceFailure_LiftsTheFreeze_SoThePointerIsNeverTrapped()
    {
        using var rig = new FreezeRig();
        rig.PointerTo(Two);
        rig.Fence.Throw = true;

        rig.Controller.ToggleFreeze();

        Assert.False(rig.Frozen.Any);
        Assert.Empty(rig.Fence.Forbidden);
    }

    [Fact]
    public void StillImageAndFrameColour_ComeFromTheSettings_AndModeAndLensAreKept()
    {
        var settings = new AppSettings { FrozenContent = FrozenContent.Still, FrozenFrameColor = "#102030", LastMode = ZoomMode.Lens };
        using var rig = new FreezeRig(settings: settings);
        rig.Models.Zoom.Toggle(Three, [One, Two, Three]);
        rig.PointerTo(Three);

        rig.Controller.ToggleFreeze();
        rig.Models.Zoom.Activate(ZoomMode.Fullscreen, Two, [One, Two, Three]);

        var frozen = rig.Frozen.For(Three)!;
        Assert.True(frozen.Still);
        Assert.Equal("#102030", frozen.FrameColor.ToHex());
        Assert.Equal(ZoomMode.Lens, frozen.Mode);
        Assert.True(frozen.Level > 1);
        Assert.Equal(Two, rig.PointerMonitor); // 960 px to both neighbours: right first
    }

    [Fact]
    public void TheDockedMonitor_IsNotFrozen_OthersAre()
    {
        using var rig = new FreezeRig();
        rig.Models.Zoom.Activate(ZoomMode.Docked, One, [One, Two, Three]);
        rig.PointerTo(One);

        rig.Controller.ToggleFreeze();
        Assert.False(rig.Frozen.Any);
        Assert.Equal(["The docked monitor cannot be frozen."], rig.Hints);

        rig.PointerTo(Two);
        rig.Controller.ToggleFreeze();
        Assert.True(rig.Frozen.IsFrozen(Two));
    }

    [Fact]
    public void WhileAnOverviewRunsOnTheMonitor_FreezingIsRefused()
    {
        using var rig = new FreezeRig();
        rig.Models.Zoom.Activate(ZoomMode.Fullscreen, One, [One, Two, Three]);
        var session = rig.Models.Overview.Press(One, ZoomMode.Fullscreen, rig.Models.Zoom.TargetFor(One));
        Assert.NotNull(session);
        rig.PointerTo(One);

        rig.Controller.ToggleFreeze();

        Assert.Same(session, rig.Models.Overview.Current);
        Assert.False(rig.Frozen.Any);
        Assert.Empty(rig.Mover.Moves);
        Assert.Equal(["End the overview first, then freeze."], rig.Hints);
    }

    [Fact]
    public void FenceEvents_AreHandledOffTheWatcherThread()
    {
        using var rig = new FreezeRig();
        var fence = new FakeFence();
        var dispatched = new List<Action>();
        using var controller = new MonitorFreezeController(rig.Settings, rig.Switch, rig.Monitors, rig.Cursor, fence, rig.Mover,
            rig.Announcer, Localization.AppTexts.CreateLocalizer(),Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            dispatched.Add);
        rig.PointerTo(One);
        controller.ToggleFreeze();

        fence.Raise(FenceTrigger.ClipLost);
        fence.Raise(FenceTrigger.DesktopSwitch);

        Assert.Single(dispatched);
        Assert.True(rig.Frozen.Any);
        dispatched[0]();
        Assert.False(rig.Frozen.Any);
    }

    [Fact]
    public void Dispose_LiftsTheFreeze_AndStopsListening()
    {
        var rig = new FreezeRig();
        rig.PointerTo(One);
        rig.Controller.ToggleFreeze();

        rig.Dispose();

        Assert.False(rig.Frozen.Any);
        Assert.Empty(rig.Fence.Forbidden);
        Assert.False(rig.Fence.HasListener);
    }
}
