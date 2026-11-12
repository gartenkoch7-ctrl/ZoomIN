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

        model.ShowEdit(One(), new IslandEditView(-1, new PhysicalRect(0, 0, 10, 10), 0), save: false);

        Assert.Equal(0, saved);
        Assert.True(model.Scene.Editing);
        Assert.True(model.Scene.Visible);

        model.ShowEdit(model.Scene.Set, null, save: true);
        Assert.Equal(1, saved);
        Assert.False(model.Scene.Editing);
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
