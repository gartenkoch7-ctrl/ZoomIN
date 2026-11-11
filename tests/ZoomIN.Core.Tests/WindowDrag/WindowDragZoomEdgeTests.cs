using ZoomIN.Core.Modes;
using ZoomIN.Core.WindowDrag;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.Tests.WindowDrag;

/// <summary>Drags that start without effect, and the report across monitors (G1, review findings).</summary>
public sealed class WindowDragZoomEdgeTests
{
    private const string Left = @"\\.\DISPLAY1";
    private const string Right = @"\\.\DISPLAY2";
    private static readonly string[] s_both = [Left, Right];

    [Fact]
    public void ZoomSwitchedOnMidDrag_IsNotLiftedByTheDrag()
    {
        var levels = new ZoomLevels();
        levels.Set(ZoomMode.Fullscreen, Right, 4, perMonitor: true);
        var model = new ZoomModel(levels, ZoomMode.Fullscreen, perMonitor: true, 0.15);
        using var drag = new WindowDragZoom(model, WindowDragOptions.Default);
        drag.Start(WindowDragKind.Move, Left); // zoom off

        model.Toggle(Right, s_both); // the user's command during the drag
        drag.PointerOn(Right);

        Assert.Equal(4, model.EffectiveTargetFor(Right));
        Assert.Equal(new WindowDragReport(WindowDragKind.Move, WindowDragAction.Lift, Right, 1, null, true, false), drag.End());
    }

    [Fact]
    public void ModeChangedFromDockedMidDrag_IsNotLiftedByTheDrag()
    {
        var model = ZoomedAt(4, ZoomMode.Docked);
        using var drag = new WindowDragZoom(model, WindowDragOptions.Default);
        drag.Start(WindowDragKind.Move, Left);

        model.Activate(ZoomMode.Fullscreen, Left, s_both);
        drag.PointerOn(Right);
        drag.MonitorsChanged();

        Assert.False(model.Temporary.IsActive(Right));
        Assert.False(drag.End()!.Value.Cleared); // nothing was held, so nothing was cleared
    }

    [Fact]
    public void Report_CountsTheLowering_OnTheMonitorWhereItShows()
    {
        var levels = new ZoomLevels();
        levels.Set(ZoomMode.Fullscreen, Left, 1.5, perMonitor: true);
        levels.Set(ZoomMode.Fullscreen, Right, 4, perMonitor: true);
        var model = new ZoomModel(levels, ZoomMode.Fullscreen, perMonitor: true, 0.15);
        model.Toggle(Left, s_both);
        using var drag = new WindowDragZoom(model, WindowDragOptions.Default with { Action = WindowDragAction.Lower });

        drag.Start(WindowDragKind.Move, Left); // 1.5× is below 2×: nothing lowered yet
        drag.PointerOn(Right);

        Assert.Equal(2, model.EffectiveTargetFor(Right));
        Assert.Equal(2.0, drag.End()!.Value.Level);
    }

    [Fact]
    public void Report_CountsTheLowering_WhenTheUserZoomsInDuringTheDrag()
    {
        var model = ZoomedAt(1.5, ZoomMode.Fullscreen);
        using var drag = new WindowDragZoom(model, WindowDragOptions.Default with { Action = WindowDragAction.Lower });
        drag.Start(WindowDragKind.Move, Left);

        model.Step(5, Left, s_both); // stays on: the ceiling stays and now lowers the view

        Assert.Equal(2, model.EffectiveTargetFor(Left));
        Assert.Equal(2.0, drag.End()!.Value.Level);
    }

    [Fact]
    public void Dispose_EndsTheDrag_AndStopsListening()
    {
        var model = ZoomedAt(4, ZoomMode.Fullscreen);
        var drag = new WindowDragZoom(model, WindowDragOptions.Default);
        drag.Start(WindowDragKind.Move, Left);

        drag.Dispose();

        Assert.False(model.Temporary.IsActive(Left));
        model.Temporary.Set(TemporaryZoomOwner.Overview, Left, 1); // no handler of the disposed drag runs
        Assert.Null(model.Temporary.LevelFor(TemporaryZoomOwner.WindowDrag, Left));
    }

    [Fact]
    public void OtherOwnersLevel_StaysWhenTheDragEnds()
    {
        var model = ZoomedAt(4, ZoomMode.Fullscreen);
        using var drag = new WindowDragZoom(model, WindowDragOptions.Default with { Action = WindowDragAction.Lower });
        model.Temporary.Set(TemporaryZoomOwner.Overview, Left, 1);

        drag.Start(WindowDragKind.Move, Left);
        drag.End();

        Assert.Equal(1, model.EffectiveTargetFor(Left));
    }

    private static ZoomModel ZoomedAt(double level, ZoomMode mode)
    {
        var levels = new ZoomLevels();
        levels.Set(mode, Left, level, perMonitor: true);
        var model = new ZoomModel(levels, mode, perMonitor: true, 0.15);
        model.Toggle(Left, s_both);
        return model;
    }
}
