using ZoomIN.Core.Modes;
using ZoomIN.Core.WindowDrag;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.Tests.WindowDrag;

/// <summary>The temporary level of a window drag on the model (G1).</summary>
public sealed class WindowDragZoomTests
{
    private const string Left = @"\\.\DISPLAY1";
    private const string Right = @"\\.\DISPLAY2";
    private const TemporaryZoomOwner Owner = TemporaryZoomOwner.WindowDrag;
    private static readonly string[] s_both = [Left, Right];

    [Fact]
    public void Drag_LiftsToOne_AndEndGivesTheLevelBack()
    {
        var model = ZoomedAt(4);
        using var drag = new WindowDragZoom(model, WindowDragOptions.Default);

        drag.Start(WindowDragKind.Move, Left);
        Assert.Equal(1, model.EffectiveTargetFor(Left));
        Assert.Equal(4, model.TargetFor(Left)); // the user's level is untouched

        var report = drag.End();

        Assert.Null(drag.End()); // over
        Assert.Equal(4, model.EffectiveTargetFor(Left));
        Assert.Equal(new WindowDragReport(WindowDragKind.Move, WindowDragAction.Lift, Left, 4, 1, false, false), report);
        Assert.Equal(4, model.LastLevelFor(ZoomMode.Fullscreen, Left));
    }

    [Theory]
    [InlineData(4, 2, 2.0)]
    [InlineData(1.5, 1.5, null)] // lower only if higher; the report says it did not lower
    public void Lower_CapsOnlyMonitorsZoomedInFurther(double level, double during, double? reported)
    {
        var model = ZoomedAt(level);
        using var drag = new WindowDragZoom(model, WindowDragOptions.Default with { Action = WindowDragAction.Lower });

        drag.Start(WindowDragKind.Resize, Left);

        Assert.Equal(during, model.EffectiveTargetFor(Left));
        Assert.Equal(reported, drag.End()!.Value.Level);
    }

    [Fact]
    public void CommandRightAfterSet_StillWins_ForTheRestOfTheDrag()
    {
        var model = ZoomedAt(4);
        using var drag = new WindowDragZoom(model, WindowDragOptions.Default);
        var once = true;
        model.Temporary.Changed += () =>
        {
            // A command arrives between the drag's Set and its check (another thread in the app).
            if (once && model.Temporary.LevelFor(Owner, Left) is not null)
            {
                once = false;
                model.Step(-10, Left, s_both); // down to 1×: clears the level there
                model.Step(10, Left, s_both);
            }
        };

        drag.Start(WindowDragKind.Move, Left);
        drag.PointerOn(Right);

        Assert.False(model.Temporary.IsActive(Right));
        Assert.True(drag.End()!.Value.Cleared);
    }

    [Fact]
    public void Unchanged_OrSwitchedOffKind_LeavesTheZoomAlone()
    {
        var model = ZoomedAt(4);
        using var unchanged = new WindowDragZoom(model, WindowDragOptions.Default with { Action = WindowDragAction.Unchanged });
        unchanged.Start(WindowDragKind.Move, Left);
        Assert.Equal(4, model.EffectiveTargetFor(Left));
        Assert.Null(unchanged.End()!.Value.Level);

        using var noResize = new WindowDragZoom(model, WindowDragOptions.Default with { OnResize = false });
        noResize.Start(WindowDragKind.Resize, Left);
        Assert.Equal(4, model.EffectiveTargetFor(Left));
    }

    [Fact]
    public void PointerOnOtherMonitor_MovesTheLevel_AndTheOldMonitorGetsItsLevelBack()
    {
        var model = ZoomedAt(4);
        using var drag = new WindowDragZoom(model, WindowDragOptions.Default);
        drag.Start(WindowDragKind.Move, Left);

        drag.PointerOn(Right);

        Assert.Equal(4, model.EffectiveTargetFor(Left));
        Assert.Equal(1, model.EffectiveTargetFor(Right));
        drag.PointerOn(Right); // same monitor: nothing
        Assert.True(drag.End()!.Value.CrossedMonitor);
        Assert.Equal(2, model.EffectiveTargetFor(Right));
    }

    [Fact]
    public void EndWithoutStart_AndPointerWithoutDrag_DoNothing()
    {
        var model = ZoomedAt(4);
        using var drag = new WindowDragZoom(model, WindowDragOptions.Default);

        drag.PointerOn(Right);
        drag.MonitorsChanged();

        Assert.Null(drag.End());
        Assert.False(model.Temporary.IsActive(Left) || model.Temporary.IsActive(Right));
    }

    [Fact]
    public void SecondStart_EndsTheFirstDrag()
    {
        var model = ZoomedAt(4);
        using var drag = new WindowDragZoom(model, WindowDragOptions.Default);
        drag.Start(WindowDragKind.Move, Left);

        drag.Start(WindowDragKind.Move, Right); // the end of the first drag was missed

        Assert.Equal(4, model.EffectiveTargetFor(Left));
        Assert.Equal(1, model.EffectiveTargetFor(Right));
        drag.End();
        Assert.False(model.Temporary.IsActive(Right));
    }

    [Fact]
    public void LensAtLower_HidesTheLens_DockedStaysUnchanged()
    {
        var lens = ZoomedAt(4, ZoomMode.Lens);
        using var lensDrag = new WindowDragZoom(lens, WindowDragOptions.Default with { Action = WindowDragAction.Lower });
        lensDrag.Start(WindowDragKind.Move, Left);
        Assert.Equal(1, lens.EffectiveTargetFor(Left));

        var docked = ZoomedAt(4, ZoomMode.Docked);
        using var dockedDrag = new WindowDragZoom(docked, WindowDragOptions.Default);
        dockedDrag.Start(WindowDragKind.Move, Left);
        Assert.Equal(4, docked.EffectiveTargetFor(Left));
        Assert.Null(dockedDrag.End()!.Value.Level);
    }

    [Fact]
    public void ZoomOff_SetsNoLevel()
    {
        var model = new ZoomModel(new ZoomLevels(), ZoomMode.Fullscreen, perMonitor: true, 0.15);
        using var drag = new WindowDragZoom(model, WindowDragOptions.Default);

        drag.Start(WindowDragKind.Move, Left);
        drag.PointerOn(Right);

        Assert.False(model.Temporary.IsActive(Left) || model.Temporary.IsActive(Right));
        Assert.Equal(new WindowDragReport(WindowDragKind.Move, WindowDragAction.Lift, Right, 1, null, true, false), drag.End());
    }

    [Fact]
    public void UserCommand_ClearsTheLevel_AndTheDragDoesNotSetItAgain()
    {
        var model = ZoomedAt(4);
        using var drag = new WindowDragZoom(model, WindowDragOptions.Default);
        drag.Start(WindowDragKind.Move, Left);

        model.Toggle(Left, s_both); // off
        model.Toggle(Left, s_both); // on again, e.g. from another thread during the drag
        drag.PointerOn(Right);

        Assert.Equal(4, model.EffectiveTargetFor(Left));
        Assert.Equal(2, model.EffectiveTargetFor(Right));
        Assert.True(drag.End()!.Value.Cleared);
    }

    [Fact]
    public void MonitorsChanged_GivesTheLevelBack_ForTheRestOfTheDrag()
    {
        var model = ZoomedAt(4);
        using var drag = new WindowDragZoom(model, WindowDragOptions.Default);
        drag.Start(WindowDragKind.Move, Left);

        drag.MonitorsChanged();
        drag.PointerOn(Right);

        Assert.False(model.Temporary.IsActive(Left) || model.Temporary.IsActive(Right));
        Assert.True(drag.End()!.Value.Cleared);
    }

    [Fact]
    public void OptionsChange_IsNormalized_Announced_AndUsedFromTheNextDrag()
    {
        var model = ZoomedAt(4);
        using var drag = new WindowDragZoom(model, WindowDragOptions.Default);
        var raised = 0;
        drag.OptionsChanged += () => raised++;
        drag.Start(WindowDragKind.Move, Left);

        drag.Options = WindowDragOptions.Default with { Action = WindowDragAction.Lower, LowerLevel = 40 };

        Assert.Equal(1, raised);
        Assert.Equal(8, drag.Options.LowerLevel);
        Assert.Equal(1, model.EffectiveTargetFor(Left)); // the running drag keeps its action
        drag.End();
        drag.Start(WindowDragKind.Move, Left);
        Assert.Equal(4, model.EffectiveTargetFor(Left)); // 8× is no lower than 4×
        Assert.Throws<ArgumentNullException>(() => drag.Options = null!);
    }

    private static ZoomModel ZoomedAt(double level, ZoomMode mode = ZoomMode.Fullscreen)
    {
        var levels = new ZoomLevels();
        levels.Set(mode, Left, level, perMonitor: true);
        var model = new ZoomModel(levels, mode, perMonitor: true, 0.15);
        model.Toggle(Left, s_both);
        return model;
    }
}
