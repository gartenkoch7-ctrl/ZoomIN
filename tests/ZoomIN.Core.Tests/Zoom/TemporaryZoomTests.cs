using ZoomIN.Core.Modes;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.Tests.Zoom;

/// <summary>Owner bookkeeping of the temporary zoom level (M17 step 0).</summary>
public sealed class TemporaryZoomTests
{
    private const string Left = @"\\.\DISPLAY1";
    private const string Right = @"\\.\DISPLAY2";
    private const TemporaryZoomOwner Overview = TemporaryZoomOwner.Overview;
    private const TemporaryZoomOwner Drag = TemporaryZoomOwner.WindowDrag;
    private static readonly string[] s_both = [Left, Right];

    [Fact]
    public void WithoutLevel_ShowsUserTarget()
    {
        var model = ZoomedAt(4);

        Assert.Equal(4, model.EffectiveTargetFor(Left));
        Assert.Equal(ZoomLimits.Max, model.Temporary.CeilingFor(Left));
        Assert.False(model.Temporary.IsActive(Left));
        Assert.Null(model.Temporary.LevelFor(Overview, Left));
    }

    [Fact]
    public void Set_LiftsOnlyThatMonitor_AndKeepsUserTarget()
    {
        var model = ZoomedAt(4);

        model.Temporary.Set(Overview, Left, 1);

        Assert.Equal(1, model.EffectiveTargetFor(Left));
        Assert.Equal(4, model.TargetFor(Left));
        Assert.Equal(2, model.EffectiveTargetFor(Right));
        Assert.True(model.IsOn);
        Assert.True(model.Temporary.IsActive(Left));
        Assert.False(model.Temporary.IsActive(Right));
        Assert.Equal(1, model.Temporary.LevelFor(Overview, Left));
    }

    [Theory]
    [InlineData(4, 2)] // higher: lowered to 2×
    [InlineData(1.5, 1.5)] // lower: unchanged (G1 "only if higher")
    [InlineData(1, 1)] // off stays off
    public void Level_IsACeiling(double user, double shown)
    {
        var model = ZoomedAt(user);

        model.Temporary.Set(Drag, Left, 2);

        Assert.Equal(shown, model.EffectiveTargetFor(Left), 9);
    }

    [Fact]
    public void Release_ReturnsToUserTarget()
    {
        var model = ZoomedAt(4);
        model.Temporary.Set(Overview, Left, 1);

        model.Temporary.Release(Overview, Left);

        Assert.Equal(4, model.EffectiveTargetFor(Left));
        Assert.False(model.Temporary.IsActive(Left));
        Assert.Null(model.Temporary.LevelFor(Overview, Left));
    }

    [Fact]
    public void TwoOwners_LowestWins_AndEachReleasesOnlyItsOwn()
    {
        var model = ZoomedAt(6);
        model.Temporary.Set(Drag, Left, 2);
        model.Temporary.Set(Overview, Left, 1);

        Assert.Equal(1, model.EffectiveTargetFor(Left));

        model.Temporary.Release(Overview, Left);
        Assert.Equal(2, model.EffectiveTargetFor(Left));

        model.Temporary.Release(Drag, Left);
        Assert.Equal(6, model.EffectiveTargetFor(Left));
    }

    [Fact]
    public void ReleaseAll_DropsOnlyThatOwnersLevels()
    {
        var model = ZoomedAt(4);
        model.Temporary.Set(Drag, Left, 1);
        model.Temporary.Set(Drag, Right, 1);
        model.Temporary.Set(Overview, Right, 1.5);

        model.Temporary.ReleaseAll(Drag);

        Assert.Equal(4, model.EffectiveTargetFor(Left));
        Assert.Equal(1.5, model.EffectiveTargetFor(Right));
        Assert.Equal(1.5, model.Temporary.LevelFor(Overview, Right));
    }

    [Fact]
    public void Move_TakesTheLevelToTheOtherMonitor_InOneChange()
    {
        var model = ZoomedAt(4);
        model.Temporary.Set(Drag, Left, 1);
        var raised = 0;
        model.Temporary.Changed += () => raised++;

        model.Temporary.Move(Drag, Left, Right);

        Assert.Equal(4, model.EffectiveTargetFor(Left));
        Assert.Equal(1, model.EffectiveTargetFor(Right));
        Assert.Equal(1, model.Temporary.LevelFor(Drag, Right));
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Move_OntoOwnLevel_KeepsTheLowerOne()
    {
        var model = ZoomedAt(4);
        model.Temporary.Set(Drag, Left, 1);
        model.Temporary.Set(Drag, Right, 1.5);

        model.Temporary.Move(Drag, Right, Left);

        Assert.Equal(1, model.Temporary.LevelFor(Drag, Left));
        Assert.False(model.Temporary.IsActive(Right));
    }

    [Fact]
    public void Move_WithoutLevel_OrToSameMonitor_DoesNothing()
    {
        var model = ZoomedAt(4);
        model.Temporary.Set(Overview, Left, 1);
        var raised = 0;
        model.Temporary.Changed += () => raised++;

        model.Temporary.Move(Drag, Left, Right); // the drag holds nothing
        model.Temporary.Move(Overview, Left, Left);

        Assert.Equal(0, raised);
        Assert.False(model.Temporary.IsActive(Right));
        Assert.Equal(1, model.EffectiveTargetFor(Left));
    }

    [Fact]
    public void Changed_IsRaisedOnlyForRealChanges_AndNeverOnTheModel()
    {
        var model = ZoomedAt(4);
        var raised = 0;
        var modelRaised = 0;
        model.Temporary.Changed += () => raised++;
        model.Changed += () => modelRaised++; // would schedule a settings save

        model.Temporary.Release(Overview, Left); // nothing held
        model.Temporary.ReleaseAll(Drag); // nothing held
        model.Temporary.Set(Overview, Left, 1);
        model.Temporary.Set(Overview, Left, 1); // same level
        model.Temporary.Set(Overview, Left, 2); // replaced
        model.Temporary.Release(Overview, Left);

        Assert.Equal(3, raised);
        Assert.Equal(0, modelRaised);
    }

    [Theory]
    [InlineData(0.5, 1)]
    [InlineData(40, 16)]
    public void Set_ClampsToZoomRange(double level, double stored)
    {
        var model = ZoomedAt(4);

        model.Temporary.Set(Overview, Left, level);

        Assert.Equal(stored, model.Temporary.LevelFor(Overview, Left));
    }

    [Fact]
    public void Set_RejectsInvalidArguments()
    {
        var model = ZoomedAt(4);

        Assert.Throws<ArgumentOutOfRangeException>(() => model.Temporary.Set(Overview, Left, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => model.Temporary.Set((TemporaryZoomOwner)99, Left, 1));
        Assert.Throws<ArgumentNullException>(() => model.Temporary.Set(Overview, null!, 1));
        Assert.Throws<ArgumentNullException>(() => model.Temporary.Move(Overview, Left, null!));
        Assert.Throws<ArgumentNullException>(() => model.Temporary.Move(Overview, null!, Left));
        Assert.Throws<ArgumentNullException>(() => model.Temporary.Release(Overview, null!));
        Assert.Throws<ArgumentNullException>(() => model.Temporary.LevelFor(Overview, null!));
        Assert.False(model.Temporary.IsActive(Left));
    }

    private static ZoomModel ZoomedAt(double level)
    {
        var levels = new ZoomLevels();
        if (level > ZoomLimits.Min)
        {
            levels.Set(ZoomMode.Fullscreen, Left, level, perMonitor: true);
        }

        var model = new ZoomModel(levels, ZoomMode.Fullscreen, perMonitor: true, 0.15);
        if (level > ZoomLimits.Min)
        {
            model.Toggle(Left, s_both);
        }

        return model;
    }
}
