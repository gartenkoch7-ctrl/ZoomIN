using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.RenderLoop;
using ZoomIN.Rendering.Tests.RenderLoop;
using ZoomIN.Rendering.TextEnhance;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>
/// M14 step 5: the budget covers the whole smart inversion – ambient pass plus what it adds to the
/// magnify pass, measured once per pipeline by PassCostProbe.
/// </summary>
[Collection(MeterTests.Name)]
public sealed class SmartCostBudgetTests
{
    private const string Monitor = @"\\.\DISPLAY1";
    private const int Size = 64;

    [Fact]
    public void AmbientAndMagnifyExtra_TogetherOverBudget_FallBack()
    {
        var health = new SmartInvertHealth();
        using var gpu = GpuDevice.CreateWarp();
        using var layer = SmartInvertLayer.Create(gpu.Device, Size, Size, Monitor, health, NullLogger.Instance);

        for (var i = 0; i < SampleMean.Count; i++)
        {
            layer.AddSample(SmartInvertLayer.BudgetMilliseconds * 0.6);
        }

        Assert.True(layer.Active); // the ambient pass alone is within the budget

        layer.SetMagnifyExtra(SmartInvertLayer.BudgetMilliseconds * 0.5);

        Assert.False(layer.Active);
        var fallback = Assert.Single(health.Fallbacks);
        Assert.Equal("budget", fallback.Reason);
        Assert.Equal(SmartInvertLayer.BudgetMilliseconds * 1.1, fallback.Milliseconds, 3);
    }

    [Fact]
    public void MagnifyExtraAlone_OverBudget_FallsBackAtOnce()
    {
        var health = new SmartInvertHealth();
        using var gpu = GpuDevice.CreateWarp();
        using var layer = SmartInvertLayer.Create(gpu.Device, Size, Size, Monitor, health, NullLogger.Instance);

        layer.SetMagnifyExtra(SmartInvertLayer.BudgetMilliseconds * 1.5);

        Assert.False(layer.Active);
    }

    [Fact]
    public void TogetherWithinBudget_StaysSmart()
    {
        var health = new SmartInvertHealth();
        using var gpu = GpuDevice.CreateWarp();
        using var layer = SmartInvertLayer.Create(gpu.Device, Size, Size, Monitor, health, NullLogger.Instance);

        layer.SetMagnifyExtra(SmartInvertLayer.BudgetMilliseconds * 0.4);
        for (var i = 0; i < SampleMean.Count; i++)
        {
            layer.AddSample(SmartInvertLayer.BudgetMilliseconds * 0.4);
        }

        Assert.True(layer.Active);
        Assert.Empty(health.Fallbacks);
    }

    [Fact]
    public void Probe_WaitsForTheGpuToWarmUp_AndRestartsFromScratch()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var probe = new PassCostProbe(gpu.Device);

        for (var i = 0; i < PassCostProbe.WarmupFrames - 1; i++)
        {
            probe.Tick();
        }

        Assert.False(probe.Wanted);
        probe.Tick();
        Assert.True(probe.Wanted);

        probe.Restart();

        Assert.False(probe.Wanted);
        Assert.False(probe.Done);
    }

    [Fact]
    public void Probe_MeasuresOncePerPipeline_AndStopsDrawing()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var image = TestImage.Create(gpu.Device, Size);
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new MagnifyPass(gpu.Device);
        using var smart = SmartInvertLayer.Create(gpu.Device, Size, Size, Monitor, new SmartInvertHealth(), NullLogger.Instance);
        using var timer = new GpuPassTimer(gpu.Device, Monitor, "magnify");
        using var probe = new PassCostProbe(gpu.Device);
        using var enhance = new EnhanceCost(gpu.Device, Monitor);
        var layer = new DesktopLayer(pass, smart, timer, probe, enhance);
        var monitor = new PhysicalRect(0, 0, Size, Size);
        var effects = new DisplayEffects(new FilterSettings(true, FilterId.InvertedSmart, 1, 0), Smoothing.Text, PointerStyle.Default);
        var frame = ModeGeometry.Compute(ZoomMode.Fullscreen, monitor, new PhysicalPoint(32, 32), 2, LensSize.Default, DockLayout.Default);

        // Flush stands in for Present. WARP runs behind the CPU, so the queries come back many
        // frames later; probe frames wait while all query sets are still out.
        for (var i = 0; i < 3_000 && !smart.MagnifyExtraReported; i++)
        {
            layer.Draw(gpu.Context, image.View, target.View, Size, Size, monitor, frame, effects, WindowRegions.Empty, false);
            gpu.Context.Flush();
        }

        Assert.True(probe.Done, $"wanted {probe.Wanted}");
        Assert.True(smart.MagnifyExtraReported, "The measured extra reaches the budget.");
        Assert.False(probe.Wanted);
        Assert.True(probe.ExtraMilliseconds >= 0);
    }
}
