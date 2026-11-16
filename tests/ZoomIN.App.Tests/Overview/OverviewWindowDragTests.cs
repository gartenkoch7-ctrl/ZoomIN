using ZoomIN.Core.Overview;
using ZoomIN.Core.WindowDrag;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Tests.Overview;

/// <summary>
/// M17 integration: the overview (B2) and a window drag (G1) hold their own temporary level on the
/// same monitor. Neither releases the other's, the lower ceiling wins, and no order leaves 1× behind.
/// </summary>
public sealed class OverviewWindowDragTests
{
    private const string First = TwoMonitors.First;

    [Fact]
    public void OverviewDuringADrag_EndsFirst_TheDragKeepsItsLevel_ThenAllGlidesBack()
    {
        using var rig = ZoomedRig();
        using var drag = new WindowDragZoom(rig.Zoom, WindowDragOptions.Default);
        var user = rig.Zoom.TargetFor(First);

        drag.Start(WindowDragKind.Move, First);
        rig.Controller.Press();
        rig.Overview.Release();
        Assert.Equal(OverviewPhase.Tapped, rig.Overview.Phase);
        Assert.Equal(ZoomLimits.Min, rig.Zoom.EffectiveTargetFor(First));

        rig.Controller.Press(); // second tap: zoom in at the frame
        Assert.Null(rig.Level());
        Assert.NotNull(rig.Zoom.Temporary.LevelFor(TemporaryZoomOwner.WindowDrag, First));
        Assert.Equal(ZoomLimits.Min, rig.Zoom.EffectiveTargetFor(First));

        // The view is placed for the level the monitor really glides to (still the drag's 1×).
        Assert.Equal(ZoomLimits.Min, rig.Models.ViewRequests.Latest!.Placement.Zoom);

        var report = drag.End();
        Assert.False(report!.Value.Cleared);
        Assert.False(rig.Zoom.Temporary.IsActive(First));
        Assert.Equal(user, rig.Zoom.EffectiveTargetFor(First));
        Assert.Equal(user, rig.Zoom.TargetFor(First));
    }

    [Fact]
    public void DragDuringAnOverview_EndsFirst_TheOverviewStays()
    {
        using var rig = ZoomedRig();
        using var drag = new WindowDragZoom(rig.Zoom, WindowDragOptions.Default);
        var user = rig.Zoom.TargetFor(First);
        rig.Controller.Press();
        rig.Overview.Release();

        drag.Start(WindowDragKind.Move, First);
        drag.End();

        Assert.Equal(OverviewPhase.Tapped, rig.Overview.Phase);
        Assert.Equal(ZoomLimits.Min, rig.Level());
        Assert.Equal(ZoomLimits.Min, rig.Zoom.EffectiveTargetFor(First));

        Assert.True(rig.Overview.Escape());
        Assert.False(rig.Zoom.Temporary.IsActive(First));
        Assert.Equal(user, rig.Zoom.EffectiveTargetFor(First));
    }

    [Fact]
    public void DragOntoTheOverviewMonitorAndBack_LeavesTheOverviewLevelAlone()
    {
        using var rig = ZoomedRig();
        using var drag = new WindowDragZoom(rig.Zoom, WindowDragOptions.Default);
        rig.Controller.Press();
        rig.Overview.Release();

        drag.Start(WindowDragKind.Move, TwoMonitors.Second);
        drag.PointerOn(First);
        drag.PointerOn(TwoMonitors.Second);
        drag.End();

        Assert.Equal(ZoomLimits.Min, rig.Level());
        Assert.Equal(OverviewPhase.Tapped, rig.Overview.Phase);
    }

    [Fact]
    public void ZoomOffDuringBoth_ClearsBoth_EndsTheOverview_AndTheDragReportsIt()
    {
        using var rig = ZoomedRig();
        using var drag = new WindowDragZoom(rig.Zoom, WindowDragOptions.Default);
        drag.Start(WindowDragKind.Move, First);
        rig.Controller.Press();
        rig.Overview.Release();

        rig.Commands.Toggle(); // zoom off: an explicit command ends every temporary view

        Assert.Equal(OverviewPhase.Off, rig.Overview.Phase);
        Assert.False(rig.Keys.Watching);
        Assert.False(rig.Zoom.Temporary.IsActive(First));
        Assert.True(drag.End()!.Value.Cleared);

        rig.Commands.Toggle(); // on again: nothing held back
        Assert.Equal(rig.Zoom.TargetFor(First), rig.Zoom.EffectiveTargetFor(First));
        Assert.True(rig.Zoom.TargetFor(First) > ZoomLimits.Min);
    }

    [Fact]
    public async Task DragsAndOverviewsOnTwoThreads_NeverDeadlock_AndLeaveNoLevel()
    {
        using var rig = ZoomedRig();
        using var drag = new WindowDragZoom(rig.Zoom, WindowDragOptions.Default);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        stop.CancelAfter(TimeSpan.FromSeconds(2));

        // The drag thread raises Temporary.Changed into the overview's handler while it holds its
        // own lock; the overview thread sets and releases its level at the same time.
        var drags = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                drag.Start(WindowDragKind.Move, First);
                drag.PointerOn(TwoMonitors.Second);
                drag.PointerOn(First);
                drag.End();
            }
        }, TestContext.Current.CancellationToken);
        var overviews = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                rig.Controller.Press();
                rig.Overview.Release();
                rig.Overview.Escape();
            }
        }, TestContext.Current.CancellationToken);

        var all = Task.WhenAll(drags, overviews);
        Assert.Same(all, await Task.WhenAny(all, Task.Delay(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken)));
        await all;

        Assert.Equal(OverviewPhase.Off, rig.Overview.Phase);
        Assert.False(rig.Zoom.Temporary.IsActive(First));
        Assert.False(rig.Zoom.Temporary.IsActive(TwoMonitors.Second));
        Assert.Equal(rig.Zoom.TargetFor(First), rig.Zoom.EffectiveTargetFor(First));
    }

    private static OverviewRig ZoomedRig()
    {
        var rig = new OverviewRig();
        rig.Commands.Toggle();
        return rig;
    }
}
