using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using Vortice.Direct3D11;
using Vortice.DXGI;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.Freeze;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.RenderLoop;
using ZoomIN.Rendering.Still;
using ZoomIN.Rendering.Tests.Gpu;
using ZoomIN.Rendering.Tests.RenderLoop;
using ZoomIN.Rendering.TextEnhance;

namespace ZoomIN.Rendering.Tests.Freeze;

/// <summary>A frozen monitor on WARP (G2): the anchor stays, live content changes, a still image does not.</summary>
[Collection(MeterTests.Name)]
public sealed class FrozenLayerTests
{
    private const int Size = 64;
    private const uint Red = 0xFFFF0000u;
    private const uint Green = 0xFF00FF00u;
    private static readonly PhysicalRect s_monitor = new(0, 0, Size, Size);

    [Fact]
    public void Live_AnchorStays_ContentChanges()
    {
        using var rig = new FrozenLayerRig();
        var frozen = Frozen(still: false);
        rig.Layer.Begin(frozen);

        var anchor = frozen.AnchorOr(new PhysicalPoint(16, 32));
        Assert.Equal(anchor, frozen.AnchorOr(new PhysicalPoint(60, 60)));
        Assert.False(rig.Layer.TryCapture(frozen, rig.Gpu.Context, rig.Source, current: true, WindowRegions.Empty));
        Assert.Equal(Red, rig.Draw(anchor));

        rig.Fill(Green);

        Assert.Equal(Green, rig.Draw(frozen.AnchorOr(new PhysicalPoint(60, 60))));
    }

    [Fact]
    public void Still_KeepsTheImage_UntilTheFreezeEnds()
    {
        using var rig = new FrozenLayerRig();
        var frozen = Frozen(still: true);
        rig.Layer.Begin(frozen);
        Assert.False(rig.Layer.HoldsStill(frozen));

        // An outdated desktop copy (after Win+L or UAC) is not taken; the next current one is.
        Assert.False(rig.Layer.TryCapture(frozen, rig.Gpu.Context, rig.Source, current: false, WindowRegions.Empty));
        Assert.True(rig.Layer.TryCapture(frozen, rig.Gpu.Context, rig.Source, current: true, WindowRegions.Empty));
        Assert.True(rig.Layer.HoldsStill(frozen));
        Assert.False(rig.Layer.TryCapture(frozen, rig.Gpu.Context, rig.Source, current: true, WindowRegions.Empty));
        rig.Fill(Green);

        Assert.Equal(Red, rig.Draw(new PhysicalPoint(16, 32)));

        // A new freeze of the same monitor takes a new image; no freeze shows the live desktop again.
        var again = Frozen(still: true);
        rig.Layer.Begin(again);
        Assert.False(rig.Layer.HoldsStill(again));
        rig.Layer.Begin(null);
        Assert.Same(rig.View, rig.Layer.Desktop(rig.View));
        Assert.Equal(Green, rig.Draw(new PhysicalPoint(16, 32)));
    }

    [Fact]
    public void RebuiltPipeline_DoesNotPassANewerDesktopOffAsTheStillImage()
    {
        using var rig = new FrozenLayerRig();
        var frozen = Frozen(still: true);
        rig.Layer.Begin(frozen);
        Assert.True(rig.Layer.TryCapture(frozen, rig.Gpu.Context, rig.Source, current: true, WindowRegions.Empty));

        using var cursor = new CursorPass(rig.Gpu.Device);
        using var rebuilt = new FrozenLayer(cursor, new StillFrame(rig.Gpu.Device, NullLogger.Instance), @"\\.\DISPLAY1", NullLogger.Instance);
        rebuilt.Begin(frozen);

        Assert.False(rebuilt.TryCapture(frozen, rig.Gpu.Context, rig.Source, current: true, WindowRegions.Empty));
        Assert.False(rebuilt.HoldsStill(frozen));
        Assert.Same(rig.View, rebuilt.Desktop(rig.View));
    }

    [Fact]
    public void SystemPointer_IsShownOnceAfterTheFreezeMovedItAway_AndWhileItIsOnTheFrozenMonitor()
    {
        using var rig = new FrozenLayerRig();
        var frozen = Frozen(still: false);

        // The jump comes first (frame still live), the freeze is seen a frame later.
        Assert.False(rig.Layer.NeedsSystemPointer(wasInside: true, onMonitor: false));
        rig.Layer.Begin(frozen);
        Assert.True(rig.Layer.NeedsSystemPointer(wasInside: false, onMonitor: false));
        Assert.False(rig.Layer.NeedsSystemPointer(wasInside: false, onMonitor: false));
        Assert.True(rig.Layer.NeedsSystemPointer(wasInside: false, onMonitor: true));

        // Live again: a pointer that only left and came back is nothing to show.
        rig.Layer.Begin(null);
        Assert.False(rig.Layer.NeedsSystemPointer(wasInside: true, onMonitor: false));
        Assert.False(rig.Layer.NeedsSystemPointer(wasInside: false, onMonitor: true));
        rig.Layer.Begin(frozen);
        Assert.False(rig.Layer.NeedsSystemPointer(wasInside: false, onMonitor: false));
    }

    [Fact]
    public void PointerArrivingOnTheFrozenMonitor_IsReportedOncePerArrival()
    {
        using var rig = new FrozenLayerRig();
        var reports = 0;
        var frozen = new FrozenMonitor(@"\\.\DISPLAY1", s_monitor, 2, ZoomMode.Fullscreen, LensSize.Default, DockLayout.Default, false,
            RgbColor.Cyan, _ => reports++);
        rig.Layer.Begin(frozen);

        rig.Layer.NeedsSystemPointer(false, onMonitor: true);
        rig.Layer.NeedsSystemPointer(false, onMonitor: true);
        Assert.Equal(1, reports);
        rig.Layer.NeedsSystemPointer(false, onMonitor: false);
        rig.Layer.NeedsSystemPointer(false, onMonitor: true);
        Assert.Equal(2, reports);
    }

    [Fact]
    public void Border_IsDrawnInTheFrameColour_OnlyAtTheEdge()
    {
        using var rig = new FrozenLayerRig();
        var frozen = Frozen(still: false);
        rig.Gpu.Context.ClearRenderTargetView(rig.Target.View, new Vortice.Mathematics.Color4(0, 0, 0, 0));

        rig.Layer.Draw(rig.Gpu.Context, rig.Target.View, s_monitor, frozen);
        rig.Layer.Draw(rig.Gpu.Context, rig.Target.View, s_monitor, null);

        var pixels = rig.Target.ReadBack(rig.Gpu.Device, rig.Gpu.Context);
        Assert.Equal(new Bgra(255, 0, 0, 255), pixels.At(1, Size / 2)); // blue, as chosen
        Assert.Equal(new Bgra(0, 0, 0, 0), pixels.At(Size / 2, Size / 2));
    }

    [Fact]
    public void PerFrameCalls_DoNotAllocate()
    {
        using var rig = new FrozenLayerRig();
        var frozen = Frozen(still: true);
        void Frame()
        {
            rig.Layer.Begin(frozen);
            if (!rig.Layer.HoldsStill(frozen))
            {
                rig.Layer.TryCapture(frozen, rig.Gpu.Context, rig.Source, current: true, WindowRegions.Empty);
            }

            _ = rig.Layer.Desktop(rig.View);
            _ = frozen.AnchorOr(new PhysicalPoint(1, 1));
            rig.Layer.Draw(rig.Gpu.Context, rig.Target.View, s_monitor, frozen);
            rig.Gpu.Context.Flush();
        }

        Frame();
        Frame();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
        {
            Frame();
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }


    private static FrozenMonitor Frozen(bool still) =>
        new(@"\\.\DISPLAY1", s_monitor, 2, ZoomMode.Fullscreen, LensSize.Default, DockLayout.Default, still, new RgbColor(0, 0, 255));
}
