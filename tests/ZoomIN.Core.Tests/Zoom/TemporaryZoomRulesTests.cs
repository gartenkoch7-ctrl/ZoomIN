using ZoomIN.Core.Modes;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.Tests.Zoom;

/// <summary>How the temporary zoom level meets user commands, saving and gliding (M17 step 0).</summary>
public sealed class TemporaryZoomRulesTests
{
    private const string Left = @"\\.\DISPLAY1";
    private const string Right = @"\\.\DISPLAY2";
    private const TemporaryZoomOwner Overview = TemporaryZoomOwner.Overview;
    private static readonly string[] s_both = [Left, Right];

    [Fact]
    public void IsNeverSaved_NeitherInLevelsNorInSettings()
    {
        var model = ZoomedAt(4);
        var before = ZoomMapping.Apply(new AppSettings(), model);

        model.Temporary.Set(Overview, Left, 1);
        var during = ZoomMapping.Apply(new AppSettings(), model);
        model.Temporary.Release(Overview, Left);

        Assert.Equal(4, model.LastLevelFor(ZoomMode.Fullscreen, Left));
        Assert.True(during.ZoomOn);
        Assert.Equal(before.MonitorZoomLevels[Left], during.MonitorZoomLevels[Left]);
        Assert.Equal(before.LastZoomLevel, during.LastZoomLevel);
    }

    [Fact]
    public void Toggle_ClearsAllLevels()
    {
        var model = ZoomedAt(4);
        model.Temporary.Set(Overview, Left, 1);
        model.Temporary.Set(TemporaryZoomOwner.WindowDrag, Right, 1);
        var raised = 0;
        var temporaryRaised = 0;
        model.Changed += () => raised++;
        model.Temporary.Changed += () => temporaryRaised++; // owners learn that their level is gone

        model.Toggle(Left, s_both); // off
        model.Toggle(Left, s_both); // on again: must not stay stuck at 1×

        Assert.Equal(4, model.EffectiveTargetFor(Left));
        Assert.Equal(2, model.EffectiveTargetFor(Right));
        Assert.Null(model.Temporary.LevelFor(Overview, Left));
        Assert.Equal(2, raised);
        Assert.Equal(1, temporaryRaised);
    }

    [Fact]
    public void SetMode_ClearsOnlyWhenTheModeChanges()
    {
        var model = ZoomedAt(4);
        model.Temporary.Set(Overview, Left, 1);

        model.SetMode(ZoomMode.Fullscreen); // same mode, e.g. a profile switch
        Assert.True(model.Temporary.IsActive(Left));

        model.SetMode(ZoomMode.Lens);
        Assert.False(model.Temporary.IsActive(Left));
    }

    [Fact]
    public void Clear_IsReported_EvenIfAChangedHandlerThrows()
    {
        var model = ZoomedAt(4);
        model.Temporary.Set(Overview, Left, 1);
        var reported = false;
        model.Changed += () => throw new InvalidOperationException("handler bug");
        model.Temporary.Changed += () => reported = true;

        Assert.Throws<InvalidOperationException>(() => model.Toggle(Left, s_both));

        Assert.True(reported);
    }

    [Fact]
    public void Docked_HasNoLevels()
    {
        var model = ZoomedAt(4);
        model.Temporary.Set(Overview, Left, 1);

        model.SetMode(ZoomMode.Docked); // switching to Docked clears
        model.Temporary.Set(Overview, Left, 1); // and Set does nothing there

        Assert.Null(model.Temporary.LevelFor(Overview, Left));
        Assert.False(model.Temporary.IsActive(Left));
        Assert.Equal(4, model.EffectiveTargetFor(Left));
    }

    [Fact]
    public void Activate_OtherMode_ClearsAllLevels()
    {
        var model = ZoomedAt(4);
        model.Temporary.Set(Overview, Left, 1);

        model.Activate(ZoomMode.Lens, Left, s_both);

        Assert.False(model.Temporary.IsActive(Left));
        Assert.Equal(model.TargetFor(Left), model.EffectiveTargetFor(Left));
    }

    [Fact]
    public void ReplaceLevels_KeepsTheLevel()
    {
        var model = ZoomedAt(4);
        model.Temporary.Set(Overview, Left, 1);

        model.ReplaceLevels(new ZoomLevels());

        Assert.Equal(1, model.EffectiveTargetFor(Left));
    }

    [Fact]
    public void Animator_GlidesToTheLevel_AndBack()
    {
        var model = ZoomedAt(4);
        var animator = Settled(model);

        model.Temporary.Set(Overview, Left, 1);
        var halfway = Frames(animator, model, 3);
        var down = Frames(animator, model, 200);
        model.Temporary.Release(Overview, Left);
        var halfwayBack = Frames(animator, model, 3);
        var back = Frames(animator, model, 200);

        Assert.InRange(halfway, 1.01, 3.99);
        Assert.Equal(1, down);
        Assert.InRange(halfwayBack, 1.01, 3.99);
        Assert.Equal(4, back);
    }

    private static ZoomAnimator Settled(ZoomModel model)
    {
        var animator = new ZoomAnimator();
        Frames(animator, model, 200);
        return animator;
    }

    // Same calls as the render loop: target from the model, then one frame at 144 Hz.
    private static double Frames(ZoomAnimator animator, ZoomModel model, int count)
    {
        for (var i = 0; i < count; i++)
        {
            animator.SetTarget(model.EffectiveTargetFor(Left));
            animator.Advance(1.0 / 144, model.AnimationSeconds);
        }

        return animator.Current;
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
