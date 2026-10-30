using System.Diagnostics;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// Render thread for one monitor (ADR-0010): own device, own pacing at that monitor's refresh
/// rate, failures stay local. Sleeps while its monitor is not zoomed (pipeline stays warm).
/// </summary>
internal sealed class MonitorRenderer : IDisposable
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    // How long a locator request keeps an otherwise idle monitor rendering.
    private const double LocatorSeconds = 1.0;

    private readonly string _deviceName;
    private readonly ZoomModel _model;
    private readonly EffectsModel _effects;
    private readonly TrackingModel _tracking;
    private readonly PointerServices _pointer;
    private readonly ILog _log;
    private readonly Action _requestRebuild;
    private readonly AutoResetEvent _wake = new(false);
    private readonly Thread _thread;
    private readonly Lock _statusGate = new();
    private ZoomStatus _status = ZoomStatus.Idle;
    private volatile bool _stopRequested;
    private volatile bool _sleeping;
    private long _heartbeat;

    public MonitorRenderer(
        string deviceName, RenderModels models, PointerServices pointer, ILog log, Action requestRebuild)
    {
        _deviceName = deviceName;
        _model = models.Zoom;
        _effects = models.Effects;
        _tracking = models.Tracking;
        _pointer = pointer;
        _log = log;
        _requestRebuild = requestRebuild;
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
                using var session = new RenderSession(_pointer, _deviceName);
                _log.Info($"Zoom pipeline ready for {_deviceName}");
                SetStatus(ZoomStatus.Idle with { IsCaptureExcluded = true });
                RunSession(session);
            }
            catch (DisplayChangedException ex)
            {
                // The monitor set may have changed: the engine rebuilds all renderers.
                _log.Info($"{_deviceName}: {ex.Message}");
                _requestRebuild();
                return;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _log.Error($"Zoom pipeline for {_deviceName} failed", ex);
                SetStatus(ZoomStatus.Idle with { LastError = ex.Message });
                var needed = _model.TargetFor(_deviceName) > ZoomLimits.Min || _effects.Current.Filter.ChangesColors;
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

        while (!_stopRequested)
        {
            Volatile.Write(ref _heartbeat, Stopwatch.GetTimestamp());
            var effects = _effects.Current;
            animator.SetTarget(_model.TargetFor(_deviceName));
            var unzoomed = animator.IsSettled && animator.Current == ZoomLimits.Min;

            // Filters work at 1× too (M5 decision); the locator needs a few frames as well.
            if (unzoomed && !effects.Filter.ChangesColors && !session.LocatorActive && !LocatorRequested())
            {
                if (session.IsVisible)
                {
                    session.Hide();
                    SetStatus(ZoomStatus.Idle with { IsCaptureExcluded = true });
                }

                // Not zoomed: sleep without capturing or rendering until the model changes.
                Sleep(Timeout.InfiniteTimeSpan);
                lastFrame = Stopwatch.GetTimestamp();
                secondStart = lastFrame;
                frames = 0;
                continue;
            }

            var now = Stopwatch.GetTimestamp();
            var delta = Math.Min(Stopwatch.GetElapsedTime(lastFrame, now).TotalSeconds, 0.1);
            lastFrame = now;
            animator.Advance(delta, _model.AnimationSeconds);

            // Unzoomed with a filter: the whole monitor, no lens or strip.
            var mode = unzoomed ? ZoomMode.Fullscreen : _model.Mode;
            var request = new FrameRequest(mode, animator.Current, _model.Lens, _model.Dock, effects,
                _effects.LocateRequestedAt, _tracking.Snapshot, _tracking.Options);
            if (session.RenderFrame(request))
            {
                frames++;
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

    // Waiting on purpose is not a hang: the render watchdog ignores sleeping threads.
    private void Sleep(TimeSpan timeout)
    {
        _sleeping = true;
        _wake.WaitOne(timeout);
        _sleeping = false;
        Volatile.Write(ref _heartbeat, Stopwatch.GetTimestamp());
    }

    private bool LocatorRequested()
    {
        var at = _effects.LocateRequestedAt;
        return at != 0 && Stopwatch.GetElapsedTime(at).TotalSeconds < LocatorSeconds;
    }

    private void SetStatus(ZoomStatus status)
    {
        lock (_statusGate)
        {
            _status = status;
        }
    }
}
