using ZoomIN.Core.Modes;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.Tests.Zoom;

/// <summary>Wheel and key steps while a temporary zoom level is active (M17 step 0).</summary>
public sealed class TemporaryZoomStepTests
{
    private const string Left = @"\\.\DISPLAY1";
    private const string Right = @"\\.\DISPLAY2";
    private const TemporaryZoomOwner Overview = TemporaryZoomOwner.Overview;
    private static readonly string[] s_both = [Left, Right];

    [Fact]
    public void Step_ChangesUserTargetUnderneath_AndKeepsTheLevel()
    {
        var model = ZoomedAt(4);
        model.Temporary.Set(Overview, Left, 1);

        model.Step(1, Left, s_both);

        Assert.Equal(5, model.TargetFor(Left), 9);
        Assert.Equal(5, model.LastLevelFor(ZoomMode.Fullscreen, Left), 9); // a user step is saved as usual
        Assert.Equal(1, model.EffectiveTargetFor(Left));

        model.Temporary.Release(Overview, Left);
        Assert.Equal(5, model.EffectiveTargetFor(Left), 9);
    }

    [Fact]
    public void Step_BelowTheLevel_ShowsAtOnce()
    {
        var model = ZoomedAt(4);
        model.Temporary.Set(TemporaryZoomOwner.WindowDrag, Left, 3);

        model.Step(-2, Left, s_both); // 4 / 1.25² = 2.56

        Assert.Equal(2.56, model.EffectiveTargetFor(Left), 9);
        Assert.True(model.Temporary.IsActive(Left));
    }

    [Fact]
    public void Step_Shared_CapsOnlyTheMonitorWithTheLevel()
    {
        var model = new ZoomModel(new ZoomLevels(), ZoomMode.Fullscreen, perMonitor: false, 0.15);
        model.Toggle(Left, s_both);
        model.Temporary.Set(TemporaryZoomOwner.WindowDrag, Left, 1);

        model.Step(1, Right, s_both);

        Assert.Equal(1, model.EffectiveTargetFor(Left));
        Assert.Equal(2.5, model.EffectiveTargetFor(Right), 9);
        Assert.Equal(2.5, model.TargetFor(Left), 9);
    }

    [Fact]
    public void Step_SwitchingAMonitorOnOrOff_ClearsItsLevelsOnly()
    {
        var model = ZoomedAt(1.25);
        model.Temporary.Set(TemporaryZoomOwner.WindowDrag, Left, 1);
        model.Temporary.Set(Overview, Right, 1);
        var temporaryRaised = 0;
        model.Temporary.Changed += () => temporaryRaised++;

        model.Step(-1, Left, s_both); // Left: 1.25× → off

        Assert.False(model.Temporary.IsActive(Left));
        Assert.True(model.Temporary.IsActive(Right));
        Assert.Equal(1, temporaryRaised);

        model.Temporary.Set(TemporaryZoomOwner.WindowDrag, Left, 1); // e.g. a drag that never ended
        model.Step(1, Left, s_both); // Left: off → 1.25×, must be visible

        Assert.Equal(1.25, model.EffectiveTargetFor(Left), 9);
        Assert.True(model.Temporary.IsActive(Right));
    }

    [Fact]
    public void Step_Shared_ClearsEveryMonitorThatSwitchesOn()
    {
        var model = new ZoomModel(new ZoomLevels(), ZoomMode.Fullscreen, perMonitor: false, 0.15);
        model.Toggle(Left, [Left]); // Right attached later: still off
        model.Temporary.Set(TemporaryZoomOwner.WindowDrag, Left, 1);
        model.Temporary.Set(TemporaryZoomOwner.WindowDrag, Right, 1);

        model.Step(1, Left, s_both); // Left stays on, Right switches on

        Assert.True(model.Temporary.IsActive(Left));
        Assert.False(model.Temporary.IsActive(Right));
        Assert.Equal(2.5, model.EffectiveTargetFor(Right), 9);
    }

    [Fact]
    public void Step_Shared_ToOff_KeepsLevelsOfMonitorsThatWereAlreadyOff()
    {
        var model = new ZoomModel(new ZoomLevels(), ZoomMode.Fullscreen, perMonitor: false, 0.15);
        model.Toggle(Left, [Left]); // Right still off
        model.Step(-3, Left, [Left]); // Left down to 1.024×
        model.Temporary.Set(TemporaryZoomOwner.WindowDrag, Right, 1);

        model.Step(-2, Left, s_both); // Left switches off; Right stays off

        Assert.Equal(1, model.TargetFor(Left));
        Assert.True(model.Temporary.IsActive(Right));
    }

    private static ZoomModel ZoomedAt(double level)
    {
        var levels = new ZoomLevels();
        levels.Set(ZoomMode.Fullscreen, Left, level, perMonitor: true);
        var model = new ZoomModel(levels, ZoomMode.Fullscreen, perMonitor: true, 0.15);
        model.Toggle(Left, s_both);
        return model;
    }
}
