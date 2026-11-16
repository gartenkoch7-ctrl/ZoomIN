namespace ZoomIN.Core.Diagnostics;

/// <summary>
/// When the render threads first had a pipeline, got a desktop copy, showed a frame and reached
/// the zoom level (end of the zoom-in animation), as timestamps of the given clock; 0 = not yet.
/// Only the secure instance wants them (M10 start timings) and calls <see cref="Arm"/>; elsewhere
/// <see cref="IsComplete"/> is true from the start, so render threads skip <see cref="Observe"/>.
/// Each is set once, by whichever monitor gets there first. No allocation.
/// </summary>
public sealed class RenderMilestones(TimeProvider time)
{
    private long _pipelineReady;
    private long _firstCapture;
    private long _firstShown;
    private long _zoomReached;
    private volatile bool _armed;

    /// <summary>The clock of all timestamps here.</summary>
    public TimeProvider Time => time;

    /// <summary>A render pipeline (device, duplication, overlay) was ready.</summary>
    public long PipelineReady => Volatile.Read(ref _pipelineReady);

    /// <summary>The first desktop copy from Desktop Duplication arrived (stamped after that frame was drawn).</summary>
    public long FirstCapture => Volatile.Read(ref _firstCapture);

    /// <summary>The first frame was presented with the overlay on screen.</summary>
    public long FirstShown => Volatile.Read(ref _firstShown);

    /// <summary>A shown frame was drawn at the zoom level itself (zoom-in animation over).</summary>
    public long ZoomReached => Volatile.Read(ref _zoomReached);

    /// <summary>Nothing (more) to record: not armed, or all happened. One read per frame then.</summary>
    public bool IsComplete =>
        !_armed || (Volatile.Read(ref _zoomReached) != 0 && Volatile.Read(ref _firstShown) != 0 && Volatile.Read(ref _firstCapture) != 0);

    /// <summary>Start recording; before the renderers start.</summary>
    public void Arm() => _armed = true;

    /// <summary>Stop recording (the start is over): the render threads skip <see cref="Observe"/> again.</summary>
    public void Disarm() => _armed = false;

    /// <summary>Render thread, once per pipeline start (not per frame).</summary>
    public void ObservePipeline()
    {
        if (_armed)
        {
            SetOnce(ref _pipelineReady);
        }
    }

    /// <summary>Render loop, after each pass while not complete: records what happened for the first time. 0 B.</summary>
    /// <param name="captured">The session holds a desktop copy.</param>
    /// <param name="shown">The overlay is on screen.</param>
    /// <param name="zoomReached">Shown, zoomed and the animation is settled at the target.</param>
    public void Observe(bool captured, bool shown, bool zoomReached)
    {
        if (captured)
        {
            SetOnce(ref _firstCapture);
        }

        if (shown)
        {
            SetOnce(ref _firstShown);
        }

        if (zoomReached)
        {
            SetOnce(ref _zoomReached);
        }
    }

    private void SetOnce(ref long field)
    {
        if (Volatile.Read(ref field) == 0)
        {
            Interlocked.CompareExchange(ref field, time.GetTimestamp(), 0);
        }
    }
}
