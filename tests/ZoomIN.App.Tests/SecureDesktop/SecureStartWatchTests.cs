using ZoomIN.App.SecureDesktop.Checks;
using ZoomIN.App.Settings;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Core.Zoom;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.App.Tests.SecureDesktop;

/// <summary>The start of the secure instance (M10, Q3): instant level, timings, then the checks.</summary>
public sealed class SecureStartWatchTests
{
    private static readonly DateTimeOffset s_processStart = DateTimeOffset.UtcNow.AddSeconds(-1);
    private static readonly SecureCheckInputs s_inputs = new(42, [], true, true);

    private readonly RenderModels _models;
    private readonly CapturingLog<SecureStartWatch> _log = new();
    private readonly CapturingLog<SecureDesktopChecks> _checksLog = new();
    private readonly SecureStartWatch _watch;

    public SecureStartWatchTests()
    {
        var settings = new AppSettings { AnimationMilliseconds = 150 };
        _models = new RenderModels(
            ZoomMapping.Create(settings), EffectsMapping.Create(settings), TrackingMapping.Create(settings),
            new WindowFilterModel(WindowFilterOptions.Default), new DebugSnapshots(), new SmartInvertHealth(),
            new OverviewModel(TimeProvider.System), new ViewCenterRequests());
        var checks = new SecureDesktopChecks(new FakeProbe(), new FakeMonitors(("DISPLAY1", new(0, 0, 1920, 1080))), _checksLog);
        _watch = new SecureStartWatch(_models, checks, new JumpingTime(), _log);
    }

    [Fact]
    public async Task AFullStart_LogsTheTimings_RestoresTheGlide_AndRunsTheChecks()
    {
        _watch.Begin();
        _watch.SkipZoomAnimation();
        Assert.Equal(0, _models.Zoom.AnimationSeconds);
        _models.Milestones.ObservePipeline();
        _models.Milestones.Observe(captured: true, shown: true, zoomReached: true);

        await _watch.FollowAsync(s_processStart, null, zoomOn: true, s_inputs, CancellationToken.None);

        Assert.Contains(_log.Lines, line => line.Id == 1107);
        Assert.Contains("pipeline after", _log.Text(1150), StringComparison.Ordinal);
        Assert.Equal(0.15, _models.Zoom.AnimationSeconds, 3);
        Assert.Contains(_checksLog.Lines, line => line.Id == 1151);
    }

    [Fact]
    public async Task AnImageNeverAtTheLevel_IsAWarning_AfterTheTimeout()
    {
        _watch.Begin();
        _watch.SkipZoomAnimation();
        _models.Milestones.Observe(captured: true, shown: true, zoomReached: false);

        await _watch.FollowAsync(s_processStart, null, zoomOn: true, s_inputs, CancellationToken.None);

        Assert.Contains(_log.Lines, line => line.Id == 1167);
        Assert.DoesNotContain(_log.Lines, line => line.Id == 1150);
        Assert.Equal(0.15, _models.Zoom.AnimationSeconds, 3);
    }

    [Fact]
    public async Task ZoomOff_ReportsNoMagnifiedImage_ButStillChecks()
    {
        _watch.Begin();
        _models.Milestones.Observe(captured: true, shown: true, zoomReached: false);

        await _watch.FollowAsync(s_processStart, null, zoomOn: false, s_inputs, CancellationToken.None);

        Assert.Empty(_log.Lines);
        Assert.Contains(_checksLog.Lines, line => line.Id == 1151);
    }

    [Fact]
    public async Task EndingDuringTheStart_StillChecks_ButReportsNoTimings()
    {
        _watch.Begin();
        _watch.SkipZoomAnimation();
        using var exiting = new CancellationTokenSource();
        await exiting.CancelAsync();

        await _watch.FollowAsync(s_processStart, null, zoomOn: true, s_inputs, exiting.Token);

        Assert.Contains(_checksLog.Lines, line => line.Id == 1151);
        Assert.Empty(_log.Lines);
        Assert.Equal(0.15, _models.Zoom.AnimationSeconds, 3);
    }

    /// <summary>Every look at the clock is two seconds later, and every wait ends at once.</summary>
    private sealed class JumpingTime : TimeProvider
    {
        private long _now;

        public override long GetTimestamp() => Interlocked.Add(ref _now, 2 * TimestampFrequency);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            // Not inline: the caller has not stored its timer yet.
            ThreadPool.QueueUserWorkItem(_ => callback(state));
            return new NoTimer();
        }

        private sealed class NoTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
