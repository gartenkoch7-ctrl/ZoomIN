using System.Diagnostics;
using ZoomIN.Core.Diagnostics;
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

    private readonly string _deviceName;
    private readonly ZoomModel _model;
    private readonly ICursorSource _cursor;
    private readonly ILog _log;
    private readonly Action _requestRebuild;
    private readonly AutoResetEvent _wake = new(false);
    private readonly Thread _thread;
    private readonly Lock _statusGate = new();
    private ZoomStatus _status = ZoomStatus.Idle;
    private volatile bool _stopRequested;

    public MonitorRenderer(string deviceName, ZoomModel model, ICursorSource cursor, ILog log, Action requestRebuild)
    {
        _deviceName = deviceName;
        _model = model;
        _cursor = cursor;
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

    public void Start() => _thread.Start();

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
                using var session = new RenderSession(_cursor, _deviceName);
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
                _wake.WaitOne(_model.TargetFor(_deviceName) > ZoomLimits.Min ? RetryDelay : Timeout.InfiniteTimeSpan);
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
            animator.SetTarget(_model.TargetFor(_deviceName));
            if (animator.IsSettled && animator.Current == ZoomLimits.Min)
            {
                if (session.IsVisible)
                {
                    session.Hide();
                    SetStatus(ZoomStatus.Idle with { IsCaptureExcluded = true });
                }

                // Not zoomed: sleep without capturing or rendering until the model changes.
                _wake.WaitOne();
                lastFrame = Stopwatch.GetTimestamp();
                secondStart = lastFrame;
                frames = 0;
                continue;
            }

            var now = Stopwatch.GetTimestamp();
            var delta = Math.Min(Stopwatch.GetElapsedTime(lastFrame, now).TotalSeconds, 0.1);
            lastFrame = now;
            animator.Advance(delta, _model.AnimationSeconds);

            if (session.RenderFrame(animator.Current))
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

    private void SetStatus(ZoomStatus status)
    {
        lock (_statusGate)
        {
            _status = status;
        }
    }
}
