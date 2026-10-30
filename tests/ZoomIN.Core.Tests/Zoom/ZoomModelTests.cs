using ZoomIN.Core.Modes;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.Tests.Zoom;

public sealed class ZoomModelTests
{
    private const string Left = @"\\.\DISPLAY1";
    private const string Right = @"\\.\DISPLAY2";
    private static readonly string[] s_both = [Left, Right];

    [Fact]
    public void StartsOff_InGivenMode()
    {
        var model = Create(mode: ZoomMode.Lens);

        Assert.False(model.IsOn);
        Assert.Equal(ZoomMode.Lens, model.Mode);
        Assert.Equal(1.0, model.TargetFor(Left));
    }

    [Fact]
    public void Toggle_PerMonitor_UsesEachMonitorsLastLevel()
    {
        var levels = new ZoomLevels();
        levels.Set(ZoomMode.Fullscreen, Left, 3.0, perMonitor: true);
        var model = Create(levels);

        model.Toggle(Left, s_both);

        Assert.Equal(3.0, model.TargetFor(Left));
        Assert.Equal(2.0, model.TargetFor(Right)); // no own level yet: shared default
    }

    [Fact]
    public void Toggle_WhenOn_SwitchesAllOff_AndKeepsLevels()
    {
        var model = Create();
        model.Step(1, Left, s_both);

        model.Toggle(Left, s_both);

        Assert.False(model.IsOn);
        Assert.Equal(1.25, model.LastLevelFor(ZoomMode.Fullscreen, Left), 9);
    }

    [Fact]
    public void Step_PerMonitor_ChangesOnlyThatMonitor()
    {
        var model = Create();
        model.Toggle(Left, s_both);

        model.Step(1, Left, s_both);

        Assert.Equal(2.5, model.TargetFor(Left), 9);
        Assert.Equal(2.0, model.TargetFor(Right), 9);
    }

    [Fact]
    public void Step_Shared_ChangesAllMonitors()
    {
        var model = Create(perMonitor: false);
        model.Toggle(Left, s_both);

        model.Step(1, Left, s_both);

        Assert.Equal(2.5, model.TargetFor(Right), 9);
        Assert.Equal(2.5, model.Read(levels => levels.Shared(ZoomMode.Fullscreen)), 9);
    }

    [Fact]
    public void StepUpFromOff_StartsAtOne_StepDownToOne_Stops()
    {
        var model = Create();

        model.Step(1, Left, s_both);
        Assert.Equal(1.25, model.TargetFor(Left), 9);

        model.Step(-1, Left, s_both);
        Assert.False(model.IsOn);
    }

    [Fact]
    public void Activate_OtherMode_SwitchesMode_WithThatModesLevel()
    {
        var model = Create();
        model.Toggle(Left, s_both);

        model.Activate(ZoomMode.Lens, Left, s_both);

        Assert.Equal(ZoomMode.Lens, model.Mode);
        Assert.Equal(3.0, model.TargetFor(Left)); // lens default
    }

    [Fact]
    public void Activate_ActiveMode_SwitchesOff()
    {
        var model = Create(mode: ZoomMode.Lens);
        model.Activate(ZoomMode.Lens, Left, s_both);

        model.Activate(ZoomMode.Lens, Left, s_both);

        Assert.False(model.IsOn);
        Assert.Equal(ZoomMode.Lens, model.Mode);
    }

    [Fact]
    public void Levels_AreKeptPerMode()
    {
        var model = Create();
        model.Activate(ZoomMode.Lens, Left, s_both);
        model.Step(2, Left, s_both);

        model.Activate(ZoomMode.Fullscreen, Left, s_both);

        Assert.Equal(2.0, model.TargetFor(Left));
        Assert.Equal(3.0 * 1.25 * 1.25, model.LastLevelFor(ZoomMode.Lens, Left), 9);
    }

    [Fact]
    public void Docked_OnlyMonitorUnderMouseIsZoomed()
    {
        var model = Create();

        model.Activate(ZoomMode.Docked, Right, s_both);

        Assert.Equal(Right, model.DockedMonitor);
        Assert.Equal(1.0, model.TargetFor(Left));
        Assert.Equal(3.0, model.TargetFor(Right));
    }

    [Fact]
    public void Docked_StepWithMouseOnOtherMonitor_ChangesDockedMonitor()
    {
        var model = Create();
        model.Activate(ZoomMode.Docked, Right, s_both);

        model.Step(1, Left, s_both);

        Assert.Equal(1.0, model.TargetFor(Left));
        Assert.Equal(3.75, model.TargetFor(Right), 9);
    }

    [Fact]
    public void Changed_IsRaised_OnlyOnRealChanges()
    {
        var model = Create();
        var raised = 0;
        model.Changed += () => raised++;

        model.Step(-1, Left, s_both); // already at 1×: no change
        model.Toggle(Left, s_both);
        model.Step(1, Left, s_both);

        Assert.Equal(2, raised);
    }

    [Fact]
    public void ResizeLens_UpdatesSize_AndRaisesChanged()
    {
        var model = Create();
        var raised = false;
        model.Changed += () => raised = true;

        model.ResizeLens(1, -1);

        Assert.Equal(new LensSize(0.45, 0.25), model.Lens);
        Assert.True(raised);
    }

    [Fact]
    public void InvalidLevels_FallBackToModeDefaults()
    {
        var levels = new ZoomLevels();
        levels.Load(ZoomMode.Lens, 0.5, new Dictionary<string, double> { [Left] = 1.0 });

        Assert.Equal(3.0, levels.Shared(ZoomMode.Lens));
        Assert.Equal(3.0, levels.Get(ZoomMode.Lens, Left, perMonitor: true));
    }

    [Fact]
    public void AnimationSeconds_IsNeverNegative()
    {
        var model = Create();
        model.AnimationSeconds = -5;

        Assert.Equal(0, model.AnimationSeconds);
    }

    private static ZoomModel Create(ZoomLevels? levels = null, bool perMonitor = true, ZoomMode mode = ZoomMode.Fullscreen) =>
        new(levels ?? new ZoomLevels(), mode, perMonitor, 0.15);
}
