namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// Lets a render thread sleep until a model changes. <see cref="Wake"/> is called from any thread
/// (zoom commands, hooks, the window drag thread) and must never fail there: during a pipeline
/// rebuild a caller may still hold the old renderer list while its renderers are disposed.
/// </summary>
internal sealed class RenderWakeup : IDisposable
{
    private readonly AutoResetEvent _event = new(false);
    private volatile bool _disposed;
    private volatile bool _sleeping;

    /// <summary>The thread waits on purpose (the render watchdog does not count it as hung).</summary>
    public bool IsSleeping => _sleeping;

    /// <summary>Wakes the thread; does nothing once disposed.</summary>
    public void Wake()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _event.Set();
        }
        catch (ObjectDisposedException)
        {
            // Disposed between the check and Set: the renderer is gone, nobody needs waking.
        }
    }

    /// <summary>
    /// Sleeps until <see cref="Wake"/> or the timeout; returns at once once disposed (a thread that
    /// outlived the 3 s join comes back after its renderer was disposed and must end, not crash).
    /// </summary>
    public void Sleep(TimeSpan timeout)
    {
        if (_disposed)
        {
            return;
        }

        _sleeping = true;
        try
        {
            _event.WaitOne(timeout);
        }
        catch (ObjectDisposedException)
        {
            // Disposed while going to sleep: the renderer is stopping anyway.
        }
        finally
        {
            _sleeping = false;
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _event.Dispose();
    }
}
