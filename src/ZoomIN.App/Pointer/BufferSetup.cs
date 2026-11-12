using Microsoft.Extensions.Logging;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.PointerFence;

namespace ZoomIN.App.Pointer;

/// <summary>
/// Feeds the mouse buffer (A6) its level and the current monitors, from settings changes and from
/// the fence's display-change notice. Its own small lock, never held around a fence call: the
/// fence's watcher thread refreshes here while the controller may hold its lock and wait for that
/// thread.
/// </summary>
internal sealed class BufferSetup(EdgeBuffer buffer, IMonitorProvider monitors, TimeProvider time, ILogger log) : IDisposable
{
    /// <summary>Retry after Windows gave no monitors (still enumerating after a display change).</summary>
    internal static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    private const int MaxRetries = 10;

    private readonly Lock _gate = new();
    private EdgeBufferLevel _level;
    private ITimer? _retry;
    private int _retries;
    private bool _disposed;

    /// <summary>Sets the level, or with null keeps it, and reads the monitors; returns how many are known.</summary>
    public int Configure(EdgeBufferLevel? level) => Configure(level, retry: false, confirm: false);

    /// <summary>
    /// After a display change: reads now and once more a second later, because Windows may still
    /// report part of the old layout (an old edge could sit in the middle of a real monitor).
    /// </summary>
    public int Refresh() => Configure(null, retry: false, confirm: true);

    private int Configure(EdgeBufferLevel? level, bool retry, bool confirm)
    {
        lock (_gate)
        {
            // A new request (settings, display change) gets the full retry budget again.
            if (!retry)
            {
                _retries = 0;
            }

            _level = level ?? _level;
            var bounds = monitors.AllBounds();

            // Empty or one monitor while Windows is still enumerating: the buffer rests (it needs
            // two) and reads again a few times. With really one monitor that costs ten cheap reads.
            buffer.Configure(bounds, _level);
            if (_level == EdgeBufferLevel.Off || _disposed || (bounds.Count >= 2 && !confirm))
            {
                _retries = 0;
            }
            else if (_retries++ < MaxRetries)
            {
                using (ExecutionContext.SuppressFlow())
                {
                    _retry ??= time.CreateTimer(_ => Retry(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                }

                _retry.Change(RetryDelay, Timeout.InfiniteTimeSpan);
            }

            return bounds.Count;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _retry?.Dispose();
            _retry = null;
        }
    }

    // On a timer thread: an escaping exception would end ZoomIN.
    private void Retry()
    {
        try
        {
            var count = Configure(null, retry: true, confirm: false);
            log.Reapplied(FenceTrigger.DisplayChange, count);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.ReapplyFailed(ex, FenceTrigger.DisplayChange);
        }
    }
}
