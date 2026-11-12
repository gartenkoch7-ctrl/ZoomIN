using ZoomIN.Core.Monitors;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Viewport;
using static ZoomIN.App.Tests.Freeze.ThreeMonitors;

namespace ZoomIN.App.Tests.Freeze;

/// <summary>Display changes while a monitor is frozen (G2): the freeze ends where it must, the pointer is never trapped.</summary>
public sealed class FreezeDisplayChangeTests
{
    [Fact]
    public void DisplayChange_LiftsTheFreezeOfAMonitorThatIsGoneOrChanged_AndKeepsTheOthers()
    {
        var monitors = new ThreeMonitors();
        using var rig = new FreezeRig(monitors);
        rig.PointerTo(Two);
        rig.Controller.ToggleFreeze();

        rig.Fence.Raise(FenceTrigger.DisplayChange);
        Assert.True(rig.Frozen.IsFrozen(Two));

        monitors.Resize(Two, new PhysicalRect(3840, 0, 2560, 1440));
        rig.Fence.Raise(FenceTrigger.DisplayChange);
        Assert.False(rig.Frozen.Any);
        Assert.Empty(rig.Fence.Forbidden);

        rig.PointerTo(One);
        rig.Controller.ToggleFreeze();
        monitors.Unplug(One);
        rig.Fence.Raise(FenceTrigger.DisplayChange);
        Assert.False(rig.Frozen.Any);
    }

    [Fact]
    public void DisplayChange_PuttingThePointerOntoTheFrozenMonitor_LiftsIt()
    {
        var monitors = new ThreeMonitors();
        using var rig = new FreezeRig(monitors);
        rig.PointerTo(Two);
        rig.Controller.ToggleFreeze();

        monitors.Unplug(Three);
        rig.PointerTo(Two); // where Windows put it
        rig.Fence.Raise(FenceTrigger.DisplayChange);

        Assert.False(rig.Frozen.Any);
        Assert.Empty(rig.Fence.Forbidden);
    }

    [Fact]
    public void DisplayChange_LeavingNoFreeMonitor_LiftsAll()
    {
        var monitors = new ThreeMonitors();
        using var rig = new FreezeRig(monitors);
        rig.PointerTo(Two);
        rig.Controller.ToggleFreeze();

        monitors.Unplug(One);
        monitors.Unplug(Three);
        rig.Fence.Raise(FenceTrigger.DisplayChange);

        Assert.False(rig.Frozen.Any);
        Assert.Empty(rig.Fence.Forbidden);
    }

    [Fact]
    public void DisplayChange_LiftsAStillImage_AndAnnouncesOnce()
    {
        using var rig = new FreezeRig(settings: new AppSettings { FrozenContent = FrozenContent.Still });
        rig.PointerTo(Two);
        rig.Controller.ToggleFreeze();
        var announced = rig.Announcer.Messages.Count;

        rig.Fence.Raise(FenceTrigger.DisplayChange);

        Assert.False(rig.Frozen.Any);
        Assert.Equal(["Monitors unfrozen"], rig.Announcer.Messages.Skip(announced));
    }

    [Fact]
    public void PointerSeenOnTheFrozenMonitor_LiftsIt_IfItIsStillThere()
    {
        using var rig = new FreezeRig();
        rig.PointerTo(Two);
        rig.Controller.ToggleFreeze();
        var frozen = rig.Frozen.For(Two)!;

        // Seen in the moment before the freeze's second move: the pointer is gone again.
        frozen.ReportPointerInside();
        Assert.True(rig.Frozen.IsFrozen(Two));

        // The fence failed (hook lost): the pointer is really on the frozen monitor.
        rig.PointerTo(Two);
        frozen.ReportPointerInside();
        Assert.False(rig.Frozen.Any);
    }
}
