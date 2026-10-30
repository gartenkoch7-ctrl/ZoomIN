using System.Diagnostics;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// Runs the render loop on its own high-priority thread, never on the UI thread (AGENTS.md 2.3).
/// Start/Stop create and destroy all resources; M2 adds animated on/off.
/// </summary>
public sealed class ZoomEngine : IZoomOutput
{
    private readonly ICursorSource _cursor;
    private readonly ILog _log;
    private readonly Lock _statusGate = new();
    private ZoomStatus _status = ZoomStatus.Stopped;
    private Thread? _thread;
    private volatile bool _stopRequested;
    private double _zoom;

    public ZoomEngine(ICursorSource cursor, ILog log)
    {
        _cursor = cursor;
        _log = log;
    }

    public bool IsRunning => _thread is { IsAlive: true };

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

    public void Start(double zoom)
    {
        if (IsRunning)
        {
            return;
        }

        _zoom = zoom;
        _stopRequested = false;
        _thread = new Thread(Run) { IsBackground = true, Name = "ZoomIN Render", Priority = ThreadPriority.Highest };
        _thread.Start();
    }

    public void Stop()
    {
        _stopRequested = true;
        _thread?.Join(TimeSpan.FromSeconds(3));
        _thread = null;
    }

    public void Dispose() => Stop();

    private void Run()
    {
        using var mmcss = MmcssScope.Enter();
        try
        {
            using var session = new RenderSession(_cursor);
            SetStatus(new ZoomStatus(true, true, 0, null));
            _log.Info($"Zoom started at {_zoom:0.##}x");
            Loop(session);
            SetStatus(ZoomStatus.Stopped);
            _log.Info("Zoom stopped");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.Error("Zoom failed", ex);
            SetStatus(ZoomStatus.Stopped with { LastError = ex.Message });
        }
    }

    private void Loop(RenderSession session)
    {
        var frames = 0;
        var totalFrames = 0L;
        var sessionStart = Stopwatch.GetTimestamp();
        var windowStart = sessionStart;
        while (!_stopRequested)
        {
            if (session.RenderFrame(_zoom))
            {
                frames++;
                totalFrames++;
            }

            // Status is published once per second; the per-frame path stays lock-free.
            if (Stopwatch.GetElapsedTime(windowStart).TotalSeconds >= 1)
            {
                SetStatus(new ZoomStatus(true, true, frames, null));
                frames = 0;
                windowStart = Stopwatch.GetTimestamp();
            }
        }

        var seconds = Stopwatch.GetElapsedTime(sessionStart).TotalSeconds;
        _log.Info($"Rendered {totalFrames} frames in {seconds:0.0} s ({totalFrames / Math.Max(seconds, 0.001):0.0} fps)");
    }

    private void SetStatus(ZoomStatus status)
    {
        lock (_statusGate)
        {
            _status = status;
        }
    }
}
