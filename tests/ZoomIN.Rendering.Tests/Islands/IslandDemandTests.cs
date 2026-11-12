using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Core.Zoom;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.Rendering.Tests.Islands;

/// <summary>When a monitor draws islands (M19 B1): read every frame, so lock-free and 0 B.</summary>
public sealed class IslandDemandTests
{
    private const string First = @"\\.\DISPLAY1";
    private const string Second = @"\\.\DISPLAY2";

    [Fact]
    public void Islands_InFullscreen_OnlyWhileZoomed_AndOnlyOnTheirMonitor()
    {
        var models = Models(ZoomMode.Fullscreen);

        Assert.Null(Draws(new OverlayDemand(First, models), 1, null));
        Assert.Same(models.Islands.Scene, Draws(new OverlayDemand(First, models), 2, null));
        Assert.Null(Draws(new OverlayDemand(Second, models), 2, null));

        models.Islands.SetVisible(false);
        Assert.Null(Draws(new OverlayDemand(First, models), 2, null));
    }

    [Theory]
    [InlineData(ZoomMode.Lens)]
    [InlineData(ZoomMode.Docked)]
    public void Islands_NotInLensOrDocked(ZoomMode mode) =>
        Assert.Null(Draws(new OverlayDemand(First, Models(mode)), 4, null));

    [Fact]
    public void EditMode_DrawsOnEveryMonitor_AtOneTimes_AndKeepsARetry()
    {
        var models = Models(ZoomMode.Lens);
        models.Islands.ShowEdit(models.Islands.Scene.Set, models.Islands.Scene.Set, new IslandEditView(-1, null, 0), save: false);
        var second = new OverlayDemand(Second, models);

        Assert.NotNull(Draws(second, 1, null));
        Assert.True(second.NeededAfterFailure());
    }

    [Fact]
    public void FrozenMonitor_KeepsItsIslandsAsTheyWere_InItsFrozenMode_UntilTheThaw()
    {
        // Frozen in fullscreen at 2×, then the live mode went to the lens: the islands stay (G2, plan B1).
        var models = Models(ZoomMode.Lens);
        var before = models.Islands.Scene;
        var frozen = Freeze(models, before);
        var first = new OverlayDemand(First, models);

        Assert.Same(before, Draws(first, 2, frozen));
        Assert.Null(Draws(first, 2, null)); // live: the lens shows no islands

        // Hidden, edited or switched elsewhere meanwhile: the frozen monitor keeps its picture.
        models.Islands.SetVisible(false);
        models.Islands.ShowEdit(models.Islands.Scene.Set, models.Islands.Scene.Set, new IslandEditView(-1, null, 0), save: false);
        Assert.Same(before, Draws(first, 2, frozen));
        Assert.Same(before, Draws(new OverlayDemand(First, models), 2, frozen)); // also after a pipeline rebuild
        Assert.True(first.NeededAfterFailure());

        // Frozen again: the islands as they are now, without the edit frames; frozen without any: none.
        Assert.Null(Draws(first, 3, Freeze(models, models.Islands.Scene)));
        Assert.Null(Draws(first, 3, Freeze(models, null)));
    }

    [Fact]
    public void FrozenDuringAnEditStart_KeepsTheIslands_WithoutTheEditFrames()
    {
        var models = Models(ZoomMode.Fullscreen);
        models.Islands.ShowEdit(models.Islands.Scene.Set, models.Islands.Scene.Set, new IslandEditView(-1, null, 0), save: false);

        var frozen = Freeze(models, models.Islands.Scene);

        Assert.NotNull(frozen.Islands);
        Assert.Null(frozen.Islands.Edit);
        Assert.Same(frozen.Islands, Draws(new OverlayDemand(First, models), 2, frozen));
        Assert.Null(Draws(new OverlayDemand(First, models), 1, frozen)); // no edit mode at 1× on it
    }

    [Fact]
    public void Islands_ReadPerFrame_DoNotAllocate()
    {
        var demand = new OverlayDemand(First, Models(ZoomMode.Fullscreen));
        Draws(demand, 2, null);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            Draws(demand, 1 + (i % 3), null);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void FrameRequest_WithIslands_NeedsTheDesktop_EvenAtOneTimes()
    {
        var effects = new DisplayEffects(FilterSettings.Default, Smoothing.Text, PointerStyle.Default);
        var request = new FrameRequest(ZoomMode.Fullscreen, ZoomMode.Fullscreen, 1, LensSize.Default, DockLayout.Default, effects, 0,
            TrackingSnapshot.Empty, TrackingOptions.Default, false, WindowRegions.Empty, false);

        Assert.True(request.OverlayOnly);
        Assert.False((request with { Islands = new IslandScene(IslandSet.Empty, true, null) }).OverlayOnly);
    }

    // As MonitorRenderer asks per frame: mode and scene of this monitor, then the islands.
    private static IslandScene? Draws(OverlayDemand demand, double zoom, FrozenMonitor? frozen) =>
        demand.Islands(zoom, demand.Mode(frozen), demand.SceneFor(frozen));

    // Frozen in fullscreen at 2× with these islands, as the freeze controller does.
    private static FrozenMonitor Freeze(RenderModels models, IslandScene? islands)
    {
        var frozen = new FrozenMonitor(First, default, 2, ZoomMode.Fullscreen, LensSize.Default, DockLayout.Default, true, default, null, islands);
        models.Zoom.Frozen.Freeze(frozen);
        return frozen;
    }

    private static RenderModels Models(ZoomMode mode)
    {
        var settings = new AppSettings { LastMode = mode };
        var islands = IslandSet.Empty.Add(new Island(0, First, new PhysicalRect(0, 0, 50, 50), new PhysicalPoint(100, 100), 2))!;
        return new RenderModels(
            ZoomMapping.Create(settings), EffectsMapping.Create(settings), TrackingMapping.Create(settings),
            new WindowFilterModel(WindowFilterOptions.Default), new DebugSnapshots(), new SmartInvertHealth(),
            new OverviewModel(TimeProvider.System), new ViewCenterRequests())
        {
            Islands = new IslandModel(islands, visible: true),
        };
    }
}
