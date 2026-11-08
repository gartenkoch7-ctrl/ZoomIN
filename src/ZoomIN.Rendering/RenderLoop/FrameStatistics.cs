using System.Diagnostics;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// Frame bookkeeping of one render thread for <see cref="RenderMetrics"/> and the status: frame
/// intervals, missed frames, time to the first visible frame and frames per second. Idle passes
/// (M14 step 3) count as neither: the gap after them is no stutter. No allocations.
/// </summary>
internal sealed class FrameStatistics(RenderMetrics metrics)
{
    private long _wokeAt;
    private long _lastPresented;
    private long _secondStart;
    private int _frames;

    /// <summary>The thread starts rendering (session start or waking from sleep).</summary>
    public void Woke(long now)
    {
        _wokeAt = now;
        _secondStart = now;
        _lastPresented = 0;
        _frames = 0;
    }

    /// <summary>When the thread woke up; with the sleep time it gives the active share.</summary>
    public long WokeAt => _wokeAt;

    /// <param name="becameVisible">The overlay appeared with this frame.</param>
    public void Record(FrameOutcome outcome, bool becameVisible)
    {
        switch (outcome)
        {
            case FrameOutcome.Presented:
                _frames++;
                var presented = Stopwatch.GetTimestamp();
                if (_lastPresented != 0)
                {
                    metrics.FramePresented(Stopwatch.GetElapsedTime(_lastPresented, presented).TotalMilliseconds);
                }

                _lastPresented = presented;
                if (becameVisible)
                {
                    metrics.Shown(Stopwatch.GetElapsedTime(_wokeAt, presented).TotalMilliseconds);
                }

                break;
            case FrameOutcome.Missed:
                metrics.FrameMissed();
                break;
            default:
                // Idle: the next presented frame starts a new interval. For the status it is a pass
                // on time, so a resting zoom does not read as "0 frames per second".
                _frames++;
                _lastPresented = 0;
                metrics.FrameSkipped();
                break;
        }
    }

    /// <summary>True once per second, with the passes on time in it: presented plus idle (status, lock-free).</summary>
    public bool SecondElapsed(out int frames)
    {
        frames = _frames;
        if (Stopwatch.GetElapsedTime(_secondStart).TotalSeconds < 1)
        {
            return false;
        }

        _frames = 0;
        _secondStart = Stopwatch.GetTimestamp();
        return true;
    }
}
