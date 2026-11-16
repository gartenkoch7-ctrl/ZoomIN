using ZoomIN.Core.Islands;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Islands;

/// <summary>Run-time state of the islands (M19 B1): events, still/remove, edit view, scene rules, drag fence.</summary>
public sealed class IslandModelTests
{
    [Fact]
    public void Changes_RaiseChanged_AndSaved_OnlyWhenSomethingChanged()
    {
        var model = new IslandModel(One(), visible: true);
        var changed = 0;
        var saved = 0;
        model.Changed += () => changed++;
        model.Saved += () => saved++;

        model.SetVisible(true);
        Assert.Equal((0, 0), (changed, saved));

        model.SetVisible(false);
        Assert.Equal((1, 1), (changed, saved));
        Assert.False(model.Scene.Visible);
    }

    [Fact]
    public void SetStill_AndRemove_WorkById()
    {
        var model = new IslandModel(One(), visible: true);
        var id = model.Scene.Set[0].Id;

        Assert.True(model.SetStill(id, true));
        Assert.True(model.Scene.Set[0].Still);
        Assert.False(model.SetStill(99, true));

        Assert.True(model.Remove(id));
        Assert.Equal(0, model.Scene.Set.Count);
        Assert.False(model.Remove(id));
    }

    [Fact]
    public void ShowEdit_DuringADrag_IsNotSaved_AndShowsTheIslands()
    {
        var model = new IslandModel(IslandSet.Empty, visible: false);
        var saved = 0;
        model.Saved += () => saved++;

        var dragged = One();
        Assert.True(model.ShowEdit(IslandSet.Empty, dragged, new IslandEditView(-1, new PhysicalRect(0, 0, 10, 10), 0), save: false));

        Assert.Equal(0, saved);
        Assert.True(model.Scene.Editing);
        Assert.Same(IslandSet.Empty, model.Committed); // a drag in progress is never saved
        Assert.False(model.Scene.Visible); // the user's choice stays; editing shows them anyway
        Assert.True(model.Scene.ShowsOn(IslandMonitors.First, fullscreenZoomed: false));

        model.EndEdit(dragged, dragged);
        Assert.Equal(1, saved);
        Assert.Same(dragged, model.Committed);
        Assert.False(model.Scene.Editing);
    }

    [Fact]
    public void ShowEdit_AfterAChangeFromOutside_PublishesNothing_AndEndEditKeepsTheOutsideChange()
    {
        var model = new IslandModel(IslandSet.Empty, visible: true);
        var view = new IslandEditView(-1, null, 0);
        model.Replace(One(), visible: true);
        var outside = model.Scene.Set;

        Assert.False(model.ShowEdit(IslandSet.Empty, IslandSet.Empty, view, save: true));
        model.EndEdit(IslandSet.Empty, IslandSet.Empty);

        Assert.Same(outside, model.Scene.Set);
    }

    [Fact]
    public void Replace_GivesNewIds_SoStillImagesAndRowsOfOldIslandsDoNotMatch()
    {
        var model = new IslandModel(One(), visible: true);
        var before = model.Scene.Set[0].Id;

        model.Replace(One(), visible: true);

        Assert.NotEqual(before, model.Scene.Set[0].Id);
        Assert.False(model.Remove(before));
    }

    [Fact]
    public void SettingsPageChange_DuringADrag_IsBuiltOnTheCommittedIslands()
    {
        var model = new IslandModel(One(), visible: true);
        var committed = model.Committed;
        var dragged = committed.Replace(0, committed[0] with { TargetOrigin = new PhysicalPoint(900, 900) });
        model.ShowEdit(committed, dragged, new IslandEditView(0, null, 0), save: false);

        model.SetStill(committed[0].Id, true);

        Assert.Equal(new PhysicalPoint(100, 100), model.Committed[0].TargetOrigin);
        Assert.True(model.Committed[0].Still);
    }

    [Fact]
    public void ChangeFromOutside_DropsTheEditView_ItBelongsToTheOldIslands()
    {
        var model = new IslandModel(One(), visible: true);
        model.ShowEdit(model.Scene.Set, model.Scene.Set, new IslandEditView(0, null, 0), save: false);

        model.Replace(IslandSet.Empty, visible: true);

        Assert.False(model.Scene.Editing);
    }

    [Fact]
    public void Replace_AfterFitTo_FitsTheProfilesIslands()
    {
        var model = new IslandModel(IslandSet.Empty, visible: true);
        model.FitTo(new IslandMonitors().BoundsOf);

        model.Replace(IslandSet.Empty.Add(new Island(0, IslandMonitors.First, new PhysicalRect(10, 10, 50, 50), new PhysicalPoint(5000, 100), 2))!, true);

        Assert.Equal(1820, model.Scene.Set[0].TargetOrigin.X);
    }

    [Fact]
    public void FitTo_PushesIslandsOntoTheirMonitor_ButKeepsTheSavedOnes()
    {
        var model = new IslandModel(
            IslandSet.Empty.Add(new Island(0, IslandMonitors.First, new PhysicalRect(10, 10, 50, 50), new PhysicalPoint(5000, 100), 2))!, true);

        model.FitTo(new IslandMonitors().BoundsOf);

        Assert.Equal(1820, model.Scene.Set[0].TargetOrigin.X);
        Assert.Equal(5000, model.Committed[0].TargetOrigin.X); // only drawn fitted: a smaller monitor for a while never shrinks the saved islands
    }

    [Theory]
    [InlineData(true, true, IslandMonitors.First, true)]
    [InlineData(true, false, IslandMonitors.First, false)]
    [InlineData(false, true, IslandMonitors.First, false)]
    [InlineData(true, true, IslandMonitors.Second, false)]
    public void ShowsOn_NeedsVisibleZoomedFullscreenAndAnIslandThere(bool visible, bool zoomed, string monitor, bool expected)
    {
        var scene = new IslandScene(One(), visible, null);

        Assert.Equal(expected, scene.ShowsOn(monitor, zoomed));
    }

    [Fact]
    public void ShowsOn_WhileEditing_EveryMonitor()
    {
        var scene = new IslandScene(IslandSet.Empty, false, new IslandEditView(-1, null, 0));

        Assert.True(scene.ShowsOn(IslandMonitors.Second, fullscreenZoomed: false));
    }

    [Fact]
    public void DragFence_ConfinesOnlyWhileADragRuns()
    {
        var fence = new IslandDragFence();
        var far = new PhysicalPoint(2500, -40);

        Assert.Equal(PointerFilterOrder.IslandEdit, fence.Order);
        Assert.Equal(far, fence.Filter(0, default, far));

        fence.Confine(IslandMonitors.FirstBounds);
        Assert.Equal(new PhysicalPoint(1919, 0), fence.Filter(0, default, far));
        Assert.Equal(new PhysicalPoint(500, 500), fence.Filter(0, default, new PhysicalPoint(500, 500)));

        fence.Confine(null);
        Assert.Equal(far, fence.Filter(0, default, far));
    }

    [Fact]
    public void DragFence_Filter_DoesNotAllocate()
    {
        var fence = new IslandDragFence();
        fence.Confine(IslandMonitors.FirstBounds);
        fence.Filter(0, default, default);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            fence.Filter(i, default, new PhysicalPoint(i, i));
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static IslandSet One() =>
        IslandSet.Empty.Add(new Island(0, IslandMonitors.First, new PhysicalRect(10, 10, 50, 50), new PhysicalPoint(100, 100), 2))!;
}
