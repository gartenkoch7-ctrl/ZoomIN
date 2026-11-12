using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Modes;
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

        Assert.Null(new OverlayDemand(First, models).Islands(1));
        Assert.Same(models.Islands.Scene, new OverlayDemand(First, models).Islands(2));
        Assert.Null(new OverlayDemand(Second, models).Islands(2));

        models.Islands.SetVisible(false);
        Assert.Null(new OverlayDemand(First, models).Islands(2));
    }

    [Theory]
    [InlineData(ZoomMode.Lens)]
    [InlineData(ZoomMode.Docked)]
    public void Islands_NotInLensOrDocked(ZoomMode mode) =>
        Assert.Null(new OverlayDemand(First, Models(mode)).Islands(4));

    [Fact]
    public void EditMode_DrawsOnEveryMonitor_AtOneTimes_AndKeepsARetry()
    {
        var models = Models(ZoomMode.Lens);
        models.Islands.ShowEdit(models.Islands.Scene.Set, models.Islands.Scene.Set, new IslandEditView(-1, null, 0), save: false);
        var second = new OverlayDemand(Second, models);

        Assert.NotNull(second.Islands(1));
        Assert.True(second.NeededAfterFailure());
    }

    [Fact]
    public void Islands_ReadPerFrame_DoNotAllocate()
    {
        var demand = new OverlayDemand(First, Models(ZoomMode.Fullscreen));
        demand.Islands(2);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            demand.Islands(1 + (i % 3));
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
        Assert.False((request with { Islands = IslandScene.Empty }).OverlayOnly);
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
