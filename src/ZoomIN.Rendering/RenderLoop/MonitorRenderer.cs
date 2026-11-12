using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Core.Zoom;
using ZoomIN.Rendering.Snapshots;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// Render thread for one monitor (ADR-0010): own device, own pacing at that monitor's refresh
/// rate, failures stay local. Sleeps while its monitor is not zoomed (pipeline stays warm).
/// </summary>
internal sealed class MonitorRenderer : IDisposable
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    private readonly string _deviceName;
    private readonly ZoomModel _model;
    private readonly EffectsModel _effects;
    private readonly TrackingModel _tracking;
    private readonly WindowFilterModel _windows;
    private readonly RenderModels _models;
    private readonly OverlayDemand _demand;
    private readonly PointerServices _pointer;
    private readonly ILogger _log;
    private readonly Action _requestRebuild;
    private readonly RenderMetrics _metrics;
    private readonly FrameStatistics _stats;
    private readonly RenderWakeup _wakeup = new();
    private readonly Thread _thread;
    private readonly Lock _statusGate = new();
    private ZoomStatus _status = ZoomStatus.Idle;
    private volatile bool _stopRequested;
    private long _heartbeat;

    public MonitorRenderer(
        string deviceName, RenderModels models, PointerServices pointer, ILogger log, Action requestRebuild)
    {
        _deviceName = deviceName;
        _model = models.Zoom;
        _effects = models.Effects;
        _tracking = models.Tracking;
        _windows = models.Windows;
        _models = models;
        _demand = new OverlayDemand(deviceName, models);
        _pointer = pointer;
        _log = log;
        _requestRebuild = requestRebuild;
        _metrics = new RenderMetrics(deviceName);
        _stats = new FrameStatistics(_metrics);
        _thread = new Thread(Run) { IsBackground = true, Name = "ZoomIN Render " + deviceName, Priority = ThreadPriority.Highest };
    }

    public ZoomStatus Status
    {
        get
        {
            lock (_statusGate)
            {
                return _status;
            }
        }
    }

    public bool IsAlive => _thread.IsAlive;

    /// <summary>
    /// True if the thread should be working but has not completed a loop for longer than
    /// <paramref name="limit"/> (e.g. stuck in a driver call). Sleeping while idle does not count.
    /// </summary>
    public bool IsHung(TimeSpan limit) =>
        _thread.IsAlive && !_wakeup.IsSleeping && Stopwatch.GetElapsedTime(Volatile.Read(ref _heartbeat)) > limit;

    public void Start()
    {
        Volatile.Write(ref _heartbeat, Stopwatch.GetTimestamp());
        _thread.Start();
    }

    /// <summary>Any thread; safe after <see cref="Dispose"/> (a rebuild may still hold the old list).</summary>
    public void Wake() => _wakeup.Wake();

    public void Dispose()
    {
        _stopRequested = true;
        _wakeup.Wake();
        if (_thread.IsAlive && Thread.CurrentThread != _thread)
        {
            _thread.Join(TimeSpan.FromSeconds(3));
        }

        _wakeup.Dispose();
    }

    private void Run()
    {
        using var mmcss = MmcssScope.Enter();
        while (!_stopRequested)
        {
            try
            {
                using var session = StartSession();
                _log.PipelineReady(_deviceName);
                _metrics.PipelineReady();
                SetStatus(ZoomStatus.Idle with { IsCaptureExcluded = true });
                RunSession(session);
            }
            catch (DisplayChangedException ex)
            {
                // The monitor set may have changed: the engine rebuilds all renderers.
                _log.DisplayChanged(_deviceName, ex.Message);
                _metrics.Restarted("display_changed");
                _requestRebuild();
                return;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _log.PipelineFailed(ex, _deviceName);
                _metrics.Restarted("failure");
                SetStatus(ZoomStatus.Idle with { LastError = ex.Message });
                Sleep(_demand.NeededAfterFailure() ? RetryDelay : Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void RunSession(RenderSession session)
    {
        var animator = new ZoomAnimator();
        var lastFrame = Stopwatch.GetTimestamp();
        _stats.Woke(lastFrame);

        while (!_stopRequested)
        {
            Volatile.Write(ref _heartbeat, Stopwatch.GetTimestamp());
            // The monitor's own colour filter, if it has one (per-monitor filter).
            var effects = _effects.CurrentFor(_deviceName);

            // Frozen (G2): the level of the moment of freezing; temporary levels and the overview pass it by.
            var frozen = _model.Frozen.For(_deviceName);
            var lowered = false;
            animator.SetTarget(frozen?.Level ?? _demand.EffectiveTarget(out lowered)); // one snapshot per frame
            var unzoomed = animator.IsSettled && animator.Current == ZoomLimits.Min;
            var windows = _windows.Regions;
            var windowsFiltered = session.FiltersWindows(windows, effects);
            var overview = frozen is null ? _demand.Overview : null;

            // Filters work at 1× too (M5 decision); so do the pointer options (larger or coloured
            // pointer, crosshair, hiding while typing); the locator needs a few frames as well.
            // The filter per window pauses the overlay while no window here needs it (ADR-0020).
            // The overview frame is drawn at 1× too (B2), so is the border of a frozen monitor (G2).
            if (unzoomed && overview is null && frozen is null && !_demand.NeedsOverlayUnzoomed(effects) && !windowsFiltered && !session.LocatorActive
                && !_demand.LocatorRequested(effects))
            {
                if (session.IsVisible)
                {
                    session.Hide(keepView: lowered);
                    SetStatus(ZoomStatus.Idle with { IsCaptureExcluded = true });
                }

                // Not zoomed: sleep without capturing or rendering until the model changes.
                var sleptAt = Stopwatch.GetTimestamp();
                _metrics.Active(Stopwatch.GetElapsedTime(_stats.WokeAt, sleptAt).TotalSeconds);
                Sleep(Timeout.InfiniteTimeSpan);
                _metrics.Paused(Stopwatch.GetElapsedTime(sleptAt).TotalSeconds);
                lastFrame = Stopwatch.GetTimestamp();
                _stats.Woke(lastFrame);
                continue;
            }

            var now = Stopwatch.GetTimestamp();
            var delta = Math.Min(Stopwatch.GetElapsedTime(lastFrame, now).TotalSeconds, 0.1);
            lastFrame = now;
            animator.Advance(delta, _model.AnimationSeconds);

            // Unzoomed: the whole monitor, no lens or strip.
            var mode = unzoomed ? ZoomMode.Fullscreen : frozen?.Mode ?? _model.Mode;
            var request = new FrameRequest(mode, _demand.AnchorMode(mode, unzoomed, overview, lowered), animator.Current,
                frozen?.Lens ?? _model.Lens, frozen?.Dock ?? _model.Dock, effects,
                frozen is null ? _effects.LocateRequestedAt : 0, frozen is null ? _tracking.Snapshot : TrackingSnapshot.Empty, _tracking.Options,
                frozen is null && _effects.PointerHiddenByTyping, // frozen: no locator, marks or pointer, so their news change nothing (G2)
                windows, windowsFiltered, overview, _model.TargetFor(_deviceName), _models.ViewRequests.PlacementFor(_deviceName),
                lowered, frozen, frozen is { Still: true, StillUnavailable: false });
            var wasVisible = session.IsVisible;
            _stats.Record(session.RenderFrame(request), !wasVisible && session.IsVisible);

            // Status is published once per second; the per-frame path stays lock-free.
            if (_stats.SecondElapsed(out var frames))
            {
                SetStatus(new ZoomStatus(session.IsVisible, true, frames, null));
            }
        }
    }

    /// <summary>Builds device, duplication and overlay as the operation "zoomin.render.pipeline_start".</summary>
    private RenderSession StartSession()
    {
        using var operation = Telemetry.Source.StartActivity("zoomin.render.pipeline_start")?.SetTag(RenderMetrics.MonitorTag, _deviceName);
        try
        {
            return new RenderSession(_pointer, _deviceName, new SnapshotTaker(_models.Snapshots, _deviceName, _log), _models.SmartInvert, _log);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The type, not the message: driver messages may name paths.
            operation?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            throw;
        }
    }

    // Waiting on purpose is not a hang: the render watchdog ignores sleeping threads.
    private void Sleep(TimeSpan timeout)
    {
        _wakeup.Sleep(timeout);
        Volatile.Write(ref _heartbeat, Stopwatch.GetTimestamp());
    }

    private void SetStatus(ZoomStatus status)
    {
        lock (_statusGate)
        {
            _status = status;
        }
    }
}
