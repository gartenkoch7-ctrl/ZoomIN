using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Core.Zoom;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.Rendering.Tests.RenderLoop;

/// <summary>What keeps an unzoomed monitor's overlay up; read every frame, so 0 B (AGENTS.md 2.3).</summary>
public sealed class OverlayDemandTests
{
    private const string First = @"\\.\DISPLAY1";

    [Fact]
    public void Overview_CountsOnlyOnItsOwnMonitor_AndKeepsARetryAfterAFailure()
    {
        var models = Models();
        var demand = new OverlayDemand(First, models);
        Assert.Null(demand.Overview);
        Assert.False(demand.NeededAfterFailure());

        models.Overview.Press(@"\\.\DISPLAY2", ZoomMode.Fullscreen, 2);
        Assert.Null(demand.Overview);

        models.Overview.Escape();
        var session = models.Overview.Press(First, ZoomMode.Fullscreen, 2);
        Assert.Same(session, demand.Overview);
        Assert.True(demand.NeededAfterFailure());
    }

    [Fact]
    public void ATemporaryOneX_IsNoModeChangeForTheAnchor()
    {
        var models = Models(ZoomMode.Lens);
        var demand = new OverlayDemand(First, models);

        // Zoom off in the lens: the unzoomed frame is the whole monitor, and so is the anchor's mode.
        Assert.False(Lowered(demand));
        Assert.Equal(ZoomMode.Fullscreen, demand.AnchorMode(ZoomMode.Fullscreen, unzoomed: true, overview: null, lowered: false));
        Assert.Equal(ZoomMode.Lens, demand.AnchorMode(ZoomMode.Lens, unzoomed: false, overview: null, lowered: false));

        // Lifted to 1× by a window drag, or the overview (also right after its end, before the release).
        models.Zoom.Toggle(First, [First]);
        models.Zoom.Temporary.Set(TemporaryZoomOwner.WindowDrag, First, 1);
        Assert.True(Lowered(demand));
        Assert.Equal(1, demand.EffectiveTarget(out _));
        Assert.Equal(ZoomMode.Lens, demand.AnchorMode(ZoomMode.Fullscreen, unzoomed: true, overview: null, lowered: true));
        models.Zoom.Temporary.ReleaseAll(TemporaryZoomOwner.WindowDrag);
        Assert.False(Lowered(demand));

        var session = models.Overview.Press(First, ZoomMode.Lens, 2);
        Assert.Equal(ZoomMode.Lens, demand.AnchorMode(ZoomMode.Fullscreen, unzoomed: true, overview: session, lowered: false));
    }

    [Fact]
    public void PerFrameChecks_DoNotAllocate()
    {
        var models = Models();
        var demand = new OverlayDemand(First, models);
        models.Overview.Press(First, ZoomMode.Fullscreen, 2);
        var effects = models.Effects.CurrentFor(First);
        var sum = 0;
        for (var i = 0; i < 100; i++)
        {
            sum += Count(demand, effects);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
        {
            sum += Count(demand, effects);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sum > 0);
    }

    private static int Count(OverlayDemand demand, DisplayEffects effects) =>
        (demand.Overview is null ? 0 : 1) + (demand.AnchorMode(ZoomMode.Fullscreen, true, demand.Overview, Lowered(demand)) == ZoomMode.Lens ? 1 : 0)
        + (demand.NeedsOverlayUnzoomed(effects) ? 1 : 0) + (demand.LocatorRequested(effects) ? 1 : 0);

    private static bool Lowered(OverlayDemand demand)
    {
        demand.EffectiveTarget(out var lowered);
        return lowered;
    }

    private static RenderModels Models(ZoomMode mode = ZoomMode.Fullscreen)
    {
        var settings = new AppSettings { LastMode = mode };
        return new RenderModels(
            ZoomMapping.Create(settings), EffectsMapping.Create(settings), TrackingMapping.Create(settings),
            new WindowFilterModel(WindowFilterOptions.Default), new DebugSnapshots(), new SmartInvertHealth(),
            new OverviewModel(TimeProvider.System), new ViewCenterRequests());
    }
}
