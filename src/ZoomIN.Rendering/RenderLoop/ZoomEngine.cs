using System.Diagnostics;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// Follows a <see cref="ZoomModel"/> on its own high-priority thread (AGENTS.md 2.3).
/// The pipeline stays warm while zoom is off: the thread sleeps on an event (no capture, no
/// rendering) and switching on is visible in the next frame (M2 decision).
/// </summary>
public sealed class ZoomEngine : IZoomOutput
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    private readonly ZoomModel _model;
    private readonly ICursorSource _cursor;
    private readonly ILog _log;
    private readonly AutoResetEvent _wake = new(false);
    private readonly Lock _statusGate = new();
    private ZoomStatus _status = ZoomStatus.Idle;
    private Thread? _thread;
    private volatile bool _stopRequested;

    public ZoomEngine(ZoomModel model, ICursorSource cursor, ILog log)
    {
        _model = model;
        _cursor = cursor;
        _log = log;
        _model.Changed += Wake;
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

    public void Start()
    {
        if (_thread is not null)
        {
            return;
        }

        _thread = new Thread(Run) { IsBackground = true, Name = "ZoomIN Render", Priority = ThreadPriority.Highest };
        _thread.Start();
    }

    public void Dispose()
    {
        _model.Changed -= Wake;
        _stopRequested = true;
        _wake.Set();
        _thread?.Join(TimeSpan.FromSeconds(3));
        _wake.Dispose();
    }

    private void Wake() => _wake.Set();

    private void Run()
    {
        using var mmcss = MmcssScope.Enter();
        while (!_stopRequested)
        {
            try
            {
                using var session = new RenderSession(_cursor);
                _log.Info("Zoom pipeline ready");
                SetStatus(ZoomStatus.Idle with { IsCaptureExcluded = true });
                RunSession(session);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // E.g. device removed or no duplication available: rebuild the pipeline.
                _log.Error("Zoom pipeline failed", ex);
                SetStatus(ZoomStatus.Idle with { LastError = ex.Message });
                _wake.WaitOne(_model.IsOn ? RetryDelay : Timeout.InfiniteTimeSpan);
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
            animator.SetTarget(_model.Target);
            if (animator.IsSettled && animator.Current == ZoomLimits.Min)
            {
                if (session.IsVisible)
                {
                    session.Hide();
                    SetStatus(ZoomStatus.Idle with { IsCaptureExcluded = true });
                }

                // Zoom is off: sleep without capturing or rendering until the model changes.
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
