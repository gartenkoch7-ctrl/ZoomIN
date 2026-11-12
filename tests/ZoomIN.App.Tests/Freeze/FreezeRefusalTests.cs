using ZoomIN.Core.Modes;
using ZoomIN.Core.Zoom;
using static ZoomIN.App.Tests.Freeze.ThreeMonitors;

namespace ZoomIN.App.Tests.Freeze;

/// <summary>Ctrl+Alt+H refuses where a frozen view would not match the screen: docked strip, overview, temporary levels (G2, M19 integration).</summary>
public sealed class FreezeRefusalTests
{
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
    public void DuringAWindowDragOnTheMonitor_FreezingIsRefused()
    {
        using var rig = new FreezeRig();
        rig.Models.Zoom.Activate(ZoomMode.Fullscreen, One, [One, Two, Three]);
        rig.Models.Zoom.Temporary.Set(TemporaryZoomOwner.WindowDrag, One, ZoomLimits.Min);
        rig.PointerTo(One);

        rig.Controller.ToggleFreeze();

        Assert.False(rig.Frozen.Any);
        Assert.Empty(rig.Mover.Moves);
        Assert.Equal(["Finish moving or resizing the window first, then freeze."], rig.Hints);
    }

    [Fact]
    public void WithAnotherTemporaryLevelOnTheMonitor_FreezingIsRefused_WithANeutralHint()
    {
        // E.g. the overview has just ended (no session any more) and still releases its level.
        using var rig = new FreezeRig();
        rig.Models.Zoom.Activate(ZoomMode.Fullscreen, One, [One, Two, Three]);
        rig.Models.Zoom.Temporary.Set(TemporaryZoomOwner.Overview, One, ZoomLimits.Min);
        rig.PointerTo(One);

        rig.Controller.ToggleFreeze();

        Assert.False(rig.Frozen.Any);
        Assert.Equal(["The monitor shows a temporary view right now. Try again in a moment."], rig.Hints);
    }

    [Fact]
    public void ATemporaryLevelThatDoesNotLowerTheMonitor_DoesNotStopTheFreeze_ButAKnownDragDoes()
    {
        using var rig = new FreezeRig();
        rig.Models.Zoom.Activate(ZoomMode.Fullscreen, One, [One, Two, Three]);
        rig.Models.Zoom.Temporary.Set(TemporaryZoomOwner.Overview, One, ZoomLimits.Max);
        rig.PointerTo(One);

        rig.Controller.ToggleFreeze();

        Assert.True(rig.Frozen.IsFrozen(One));
        Assert.Empty(rig.Hints);

        // A known window drag refuses even if it does not lower the monitor: the jump would take the window along.
        rig.Controller.ToggleFreeze();
        rig.PointerTo(One);
        rig.Models.Zoom.Temporary.Set(TemporaryZoomOwner.WindowDrag, One, ZoomLimits.Max);

        rig.Controller.ToggleFreeze();

        Assert.False(rig.Frozen.Any);
        Assert.Equal(["Finish moving or resizing the window first, then freeze."], rig.Hints);
    }
}
