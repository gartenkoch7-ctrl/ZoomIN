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
    private readonly PointerServices _pointer;
    private readonly ILogger _log;
    private readonly Action _requestRebuild;
    private readonly RenderMetrics _metrics;
    private readonly AutoResetEvent _wake = new(false);
    private readonly Thread _thread;
    private readonly Lock _statusGate = new();
    private ZoomStatus _status = ZoomStatus.Idle;
    private volatile bool _stopRequested;
    private volatile bool _sleeping;
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
        _pointer = pointer;
        _log = log;
        _requestRebuild = requestRebuild;
        _metrics = new RenderMetrics(deviceName);
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
        _thread.IsAlive && !_sleeping && Stopwatch.GetElapsedTime(Volatile.Read(ref _heartbeat)) > limit;

    public void Start()
    {
        Volatile.Write(ref _heartbeat, Stopwatch.GetTimestamp());
        _thread.Start();
    }

    public void Wake() => _wake.Set();

    public void Dispose()
    {
        _stopRequested = true;
        _wake.Set();
        if (_thread.IsAlive && Thread.CurrentThread != _thread)
        {
            _thread.Join(TimeSpan.FromSeconds(3));
        }

        _wake.Dispose();
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
                var needed = _model.TargetFor(_deviceName) > ZoomLimits.Min || NeedsOverlayUnzoomed(_effects.CurrentFor(_deviceName))
                    || _windows.Regions.Count > 0;
                Sleep(needed ? RetryDelay : Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void RunSession(RenderSession session)
    {
        var animator = new ZoomAnimator();
        var frames = 0;
        var lastFrame = Stopwatch.GetTimestamp();
        var secondStart = lastFrame;

        // Measurements (RenderMetrics): when the thread woke up without a visible overlay, and
        // when the last frame was presented (0 = none since waking).
        var wokeAt = lastFrame;
        var lastPresented = 0L;

        while (!_stopRequested)
        {
            Volatile.Write(ref _heartbeat, Stopwatch.GetTimestamp());
            // The monitor's own colour filter, if it has one (per-monitor filter).
            var effects = _effects.CurrentFor(_deviceName);
            animator.SetTarget(_model.TargetFor(_deviceName));
            var unzoomed = animator.IsSettled && animator.Current == ZoomLimits.Min;
            var windows = _windows.Regions;
            var windowsFiltered = session.FiltersWindows(windows, effects);

            // Filters work at 1× too (M5 decision); so do the pointer options (larger or coloured
            // pointer, crosshair, hiding while typing); the locator needs a few frames as well.
            // The filter per window pauses the overlay while no window here needs it (ADR-0020).
            if (unzoomed && !NeedsOverlayUnzoomed(effects) && !windowsFiltered && !session.LocatorActive && !LocatorRequested(effects))
            {
                if (session.IsVisible)
                {
                    session.Hide();
                    SetStatus(ZoomStatus.Idle with { IsCaptureExcluded = true });
                }

                // Not zoomed: sleep without capturing or rendering until the model changes.
                var sleptAt = Stopwatch.GetTimestamp();
                _metrics.Active(Stopwatch.GetElapsedTime(wokeAt, sleptAt).TotalSeconds);
                Sleep(Timeout.InfiniteTimeSpan);
                _metrics.Paused(Stopwatch.GetElapsedTime(sleptAt).TotalSeconds);
                lastFrame = Stopwatch.GetTimestamp();
                secondStart = lastFrame;
                wokeAt = lastFrame;
                lastPresented = 0;
                frames = 0;
                continue;
            }

            var now = Stopwatch.GetTimestamp();
            var delta = Math.Min(Stopwatch.GetElapsedTime(lastFrame, now).TotalSeconds, 0.1);
            lastFrame = now;
            animator.Advance(delta, _model.AnimationSeconds);

            // Unzoomed: the whole monitor, no lens or strip.
            var mode = unzoomed ? ZoomMode.Fullscreen : _model.Mode;
            var request = new FrameRequest(mode, animator.Current, _model.Lens, _model.Dock, effects,
                _effects.LocateRequestedAt, _tracking.Snapshot, _tracking.Options, _effects.PointerHiddenByTyping,
                windows, windowsFiltered);
            var wasVisible = session.IsVisible;
            if (session.RenderFrame(request))
            {
                frames++;
                var presented = Stopwatch.GetTimestamp();
                if (lastPresented != 0)
                {
                    _metrics.FramePresented(Stopwatch.GetElapsedTime(lastPresented, presented).TotalMilliseconds);
                }

                lastPresented = presented;
                if (!wasVisible && session.IsVisible)
                {
                    _metrics.Shown(Stopwatch.GetElapsedTime(wokeAt, presented).TotalMilliseconds);
                }
            }
            else
            {
                _metrics.FrameMissed();
            }

            // Status is published once per second; the per-frame path stays lock-free.
            if (Stopwatch.GetElapsedTime(secondStart).TotalSeconds >= 1)
            {
                SetStatus(new ZoomStatus(session.IsVisible, true, frames, null));
                frames = 0;
                secondStart = Stopwatch.GetTimestamp();
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
        _sleeping = true;
        _wake.WaitOne(timeout);
        _sleeping = false;
        Volatile.Write(ref _heartbeat, Stopwatch.GetTimestamp());
    }

    private bool NeedsOverlayUnzoomed(DisplayEffects effects) =>
        effects.Filter.ChangesColors || effects.Cursor.NeedsOverlayUnzoomed || _effects.PointerHiddenByTyping;

    // A locator request keeps an otherwise idle monitor rendering until the ring is gone.
    private bool LocatorRequested(DisplayEffects effects)
    {
        var at = _effects.LocateRequestedAt;
        return at != 0 && Stopwatch.GetElapsedTime(at).TotalSeconds < effects.Cursor.LocatorSeconds;
    }

    private void SetStatus(ZoomStatus status)
    {
        lock (_statusGate)
        {
            _status = status;
        }
    }
}
