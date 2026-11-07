using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.Rendering.Tests.RenderLoop;

/// <summary>GPU time per pass (zoomin.render.gpu_time, M14 step 1): a mean over 120 frames, not per frame.</summary>
public sealed class GpuPassTimerTests
{
    [Fact]
    public void Mean_IsPublishedOnlyAfterAFullSet()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var timer = new GpuPassTimer(gpu.Device, @"\\.\DISPLAY1", "magnify");

        for (var i = 0; i < 119; i++)
        {
            timer.AddSample(2.0);
        }

        Assert.Equal(0, timer.LastMeanMilliseconds);

        timer.AddSample(2.0);

        Assert.Equal(2.0, timer.LastMeanMilliseconds, 6);
    }

    [Fact]
    public void Mean_StartsOverAfterEachSet()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var timer = new GpuPassTimer(gpu.Device, @"\\.\DISPLAY1", "magnify");

        for (var i = 0; i < 120; i++)
        {
            timer.AddSample(1.0);
        }

        for (var i = 0; i < 120; i++)
        {
            timer.AddSample(i < 60 ? 2.0 : 4.0);
        }

        Assert.Equal(3.0, timer.LastMeanMilliseconds, 6);
    }

    [Fact]
    public void BeginEndCollect_WithoutFinishedQueries_ChangesNothing()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var timer = new GpuPassTimer(gpu.Device, @"\\.\DISPLAY1", "magnify");

        // End without Begin (e.g. every query set still pending) must not break the queries.
        timer.End(gpu.Context);
        timer.Begin(gpu.Context);
        timer.End(gpu.Context);
        timer.Collect(gpu.Context);

        Assert.Equal(0, timer.LastMeanMilliseconds);
    }
}
