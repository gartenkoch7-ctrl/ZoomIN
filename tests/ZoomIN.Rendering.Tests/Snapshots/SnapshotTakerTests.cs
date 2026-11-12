using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.RenderLoop;
using ZoomIN.Rendering.Snapshots;
using ZoomIN.Rendering.Tests.Gpu;

namespace ZoomIN.Rendering.Tests.Snapshots;

/// <summary>Debug snapshots on WARP: files on request, once per request, nothing per frame otherwise.</summary>
public sealed class SnapshotTakerTests
{
    private const int Size = 32;
    private const string Monitor = @"\\.\DISPLAY1";
    private static readonly PhysicalRect s_monitor = new(0, 0, Size, Size);
    private static readonly ModeFrame s_frame = new(s_monitor, s_monitor, false);

    [Fact]
    public async Task Request_SavesOriginalResultWeightsAndInfo_Once()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var image = TestImage.Create(gpu.Device, Size);
        using var target = RenderTarget.Create(gpu.Device, Size);
        using var ambient = new AmbientPass(gpu.Device, Size, Size);
        ambient.Update(gpu.Context, image.View, 1, protectPhotos: true);
        var snapshots = new DebugSnapshots();
        var taker = new SnapshotTaker(snapshots, Monitor, NullLogger.Instance);
        var done = new TaskCompletionSource<DebugSnapshotResult>();
        var reports = 0;
        snapshots.Completed += r => { Interlocked.Increment(ref reports); done.TrySetResult(r); };
        var folder = Path.Combine(Path.GetTempPath(), $"zoomin-snapshot-{Guid.NewGuid():N}");
        try
        {
            snapshots.Request(folder);
            taker.TakeIfRequested(gpu.Device, gpu.Context, image.View, target.View, ambient.Weights, s_monitor, s_frame, Request());
            taker.TakeIfRequested(gpu.Device, gpu.Context, image.View, target.View, ambient.Weights, s_monitor, s_frame, Request());
            var result = await done.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

            Assert.True(result.Saved, result.Reason);
            Assert.Equal(1, reports);
            foreach (var file in new[] { "DISPLAY1-original.png", "DISPLAY1-result.png", "DISPLAY1-weights.png", "DISPLAY1-info.txt" })
            {
                Assert.True(File.Exists(Path.Combine(folder, file)), file);
            }

            Assert.Contains("InvertedSmart", await File.ReadAllTextAsync(Path.Combine(folder, "DISPLAY1-info.txt"), TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Fact]
    public void NothingCaptured_ReportsNothingDrawn()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var target = RenderTarget.Create(gpu.Device, Size);
        var snapshots = new DebugSnapshots();
        DebugSnapshotResult? seen = null;
        snapshots.Completed += r => seen = r;

        snapshots.Request(Path.GetTempPath());
        new SnapshotTaker(snapshots, Monitor, NullLogger.Instance)
            .TakeIfRequested(gpu.Device, gpu.Context, null, target.View, null, s_monitor, s_frame, Request());

        Assert.False(seen?.Saved);
        Assert.Equal("nothing_drawn", seen?.Reason);
    }

    [Fact]
    public void WithoutNewRequest_DoesNotAllocate()
    {
        using var gpu = GpuDevice.CreateWarp();
        using var image = TestImage.Create(gpu.Device, Size);
        using var target = RenderTarget.Create(gpu.Device, Size);
        var taker = new SnapshotTaker(new DebugSnapshots(), Monitor, NullLogger.Instance);
        var request = Request();
        taker.TakeIfRequested(gpu.Device, gpu.Context, image.View, target.View, null, s_monitor, s_frame, request);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
        {
            taker.TakeIfRequested(gpu.Device, gpu.Context, image.View, target.View, null, s_monitor, s_frame, request);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static FrameRequest Request() => new(
        ZoomMode.Fullscreen, 1, LensSize.Default, DockLayout.Default,
        new DisplayEffects(new FilterSettings(true, FilterId.InvertedSmart, 1, 0), Smoothing.Text, PointerStyle.Default),
        0, TrackingSnapshot.Empty, TrackingOptions.Default, false,
        new WindowRegions([new PhysicalRect(0, 0, 16, 16)], [WindowPalette.Smart]), true);
}
