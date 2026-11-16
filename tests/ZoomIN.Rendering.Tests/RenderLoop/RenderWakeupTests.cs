using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Core.Zoom;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.Rendering.Tests.RenderLoop;

/// <summary>
/// A pipeline rebuild disposes renderers while other threads (zoom commands, the window drag
/// thread) may still wake them from the old list: waking must never throw there (M17 integration).
/// </summary>
public sealed class RenderWakeupTests
{
    [Fact]
    public void Wake_AfterDispose_DoesNothing()
    {
        var wakeup = new RenderWakeup();
        wakeup.Dispose();

        Assert.Null(Record.Exception(wakeup.Wake));
    }

    [Fact]
    public async Task Wake_WhileDisposing_NeverThrows()
    {
        for (var i = 0; i < 200; i++)
        {
            var wakeup = new RenderWakeup();
            using var go = new ManualResetEventSlim();
            var waker = Task.Run(() =>
            {
                go.Wait(TestContext.Current.CancellationToken);
                for (var j = 0; j < 100; j++)
                {
                    wakeup.Wake();
                }
            }, TestContext.Current.CancellationToken);

            go.Set();
            wakeup.Dispose();
            await waker;
        }
    }

    [Fact]
    public void Sleep_AfterDispose_ReturnsAtOnce()
    {
        var wakeup = new RenderWakeup();
        wakeup.Dispose();

        Assert.Null(Record.Exception(() => wakeup.Sleep(Timeout.InfiniteTimeSpan)));
        Assert.False(wakeup.IsSleeping);
    }

    [Fact]
    public async Task Sleep_EndsOnWake_AndIsNotSleepingAfterwards()
    {
        using var wakeup = new RenderWakeup();
        var sleeper = Task.Run(() => wakeup.Sleep(TimeSpan.FromSeconds(10)), TestContext.Current.CancellationToken);
        SpinWait.SpinUntil(() => wakeup.IsSleeping, TimeSpan.FromSeconds(5));

        wakeup.Wake();

        await sleeper.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(wakeup.IsSleeping);
    }

    [Fact]
    public void MonitorRenderer_Wake_AfterDispose_DoesNotThrow()
    {
        // Not started: Dispose frees the wake-up at once, as after a rebuild.
        var settings = new AppSettings();
        var models = new RenderModels(
            ZoomMapping.Create(settings), EffectsMapping.Create(settings), TrackingMapping.Create(settings),
            new WindowFilterModel(WindowFilterOptions.Default), new DebugSnapshots(), new SmartInvertHealth(),
            new OverviewModel(TimeProvider.System), new ViewCenterRequests());
        var renderer = new MonitorRenderer(@"\\.\DISPLAY1", models, null!, NullLogger.Instance, () => { });
        renderer.Dispose();

        Assert.Null(Record.Exception(renderer.Wake));
    }
}
