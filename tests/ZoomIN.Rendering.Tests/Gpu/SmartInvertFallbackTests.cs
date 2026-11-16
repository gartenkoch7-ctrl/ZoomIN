using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>ADR-0024 decision 6: without the GPU feature or over budget, smart shows "keep hue".</summary>
public sealed class SmartInvertFallbackTests
{
    private const string Monitor = @"\\.\DISPLAY1";
    private const int Size = 64;

    [Fact]
    public void WithoutWeights_FallsBackAtOnce_AndSaysWhy()
    {
        var health = new SmartInvertHealth();
        using var layer = new SmartInvertLayer(null, null, Monitor, health, NullLogger.Instance);

        Assert.False(layer.Active);
        Assert.Equal("gpu_feature", Assert.Single(health.Fallbacks).Reason);
    }

    [Fact]
    public void SlowerThanTheBudget_FallsBack_OnlyAfterSeveralMeansInARow()
    {
        var health = new SmartInvertHealth();
        using var gpu = GpuDevice.CreateWarp();
        using var layer = SmartInvertLayer.Create(gpu.Device, Size, Size, Monitor, health, NullLogger.Instance);
        var slow = layer.BudgetMilliseconds * 2;
        var frames = SampleMean.Count * SmartBudget.Strikes;

        for (var i = 0; i < frames - 1; i++)
        {
            layer.AddSample(slow);
        }

        Assert.True(layer.Active); // a slow phase shorter than the strikes never decides
        layer.AddSample(slow);

        Assert.False(layer.Active);
        var fallback = Assert.Single(health.Fallbacks);
        Assert.Equal("budget", fallback.Reason);
        Assert.Equal(slow, fallback.Milliseconds, 3);
    }

    [Fact]
    public void AFastMeanInBetween_StartsTheCountAgain()
    {
        var health = new SmartInvertHealth();
        using var gpu = GpuDevice.CreateWarp();
        using var layer = SmartInvertLayer.Create(gpu.Device, Size, Size, Monitor, health, NullLogger.Instance);

        for (var round = 0; round < 4; round++)
        {
            for (var i = 0; i < SampleMean.Count * (SmartBudget.Strikes - 1); i++)
            {
                layer.AddSample(layer.BudgetMilliseconds * 2);
            }

            for (var i = 0; i < SampleMean.Count; i++)
            {
                layer.AddSample(layer.BudgetMilliseconds / 2);
            }
        }

        Assert.True(layer.Active);
    }

    [Fact]
    public void WithinTheBudget_StaysSmart()
    {
        var health = new SmartInvertHealth();
        using var gpu = GpuDevice.CreateWarp();
        using var layer = SmartInvertLayer.Create(gpu.Device, Size, Size, Monitor, health, NullLogger.Instance);

        for (var i = 0; i < SampleMean.Count * SmartBudget.Strikes * 2; i++)
        {
            layer.AddSample(layer.BudgetMilliseconds / 2);
        }

        Assert.True(layer.Active);
        Assert.Empty(health.Fallbacks);
    }

    [Fact]
    public void AfterAFallback_SmartWindowsShowKeepHue()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var image = TestImage.Create(gpu.Device, Size, (_, _) => new Bgra(32, 32, 32, 255));
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var pass = new MagnifyPass(gpu.Device);
        var monitor = new PhysicalRect(0, 0, Size, Size);
        var regions = new WindowRegions([monitor], [WindowPalette.Smart]);
        var windows = default(WindowConstants);
        WindowConstants.Fill(ref windows, regions, monitor, DisplayEffects.Default, smartActive: false);
        pass.UpdateWindows(gpu.Context, windows);

        // No weights at all: a dark area is inverted like "keep hue" instead of staying dark.
        pass.Draw(gpu.Context, image.View, target.View, Size, Size,
            MagnifyConstants.Fullscreen(monitor, monitor, DisplayEffects.Default.Matrix, Smoothing.Pixel));

        Assert.True(target.ReadBack(gpu.Device, gpu.Context).At(32, 32).R > 200);
    }

    [Fact]
    public void Health_KeepsOneEntryPerMonitor()
    {
        var health = new SmartInvertHealth();

        health.Report(new SmartInvertFallback(Monitor, "budget", 1.5));
        health.Report(new SmartInvertFallback(Monitor, "budget", 2.5));
        health.Report(new SmartInvertFallback(@"\\.\DISPLAY2", "gpu_feature", 0));

        Assert.Equal(2, health.Fallbacks.Count);
        Assert.Equal(2.5, health.Fallbacks.Single(f => f.Monitor == Monitor).Milliseconds);
    }
}
