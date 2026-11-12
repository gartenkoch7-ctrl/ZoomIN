using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Overview;

namespace ZoomIN.App.Overview;

/// <summary>
/// The keyboard hook's signals for the overview (B2): moved off the hook thread at once and handed
/// to <see cref="OverviewModel"/> one after the other, in the order the hook saw them (a hold
/// release then a quick Esc must stay a zoom-in; the watch itself sends no release after an Esc).
/// The model checks the overview's id under its lock,
/// so a late signal of an overview that already ended never touches the next one.
/// </summary>
internal sealed class OverviewKeySignals(OverviewModel overview, TimeProvider time, ILogger log) : IOverviewKeyHandler
{
    private readonly ConcurrentQueue<(long Id, bool Escape, long? ReleasedAt)> _pending = new();
    private int _draining;

    // The release time is taken on the hook thread: a late pool thread must not turn a tap into a
    // hold. A late release (found by the key state) has no time: it counts as a tap.
    public void ShortcutReleased(long watchId, bool late) => Post((watchId, false, late ? null : time.GetTimestamp()));

    public void EscapePressed(long watchId) => Post((watchId, true, null));

    private void Post((long Id, bool Escape, long? ReleasedAt) signal)
    {
        _pending.Enqueue(signal);
        if (Interlocked.CompareExchange(ref _draining, 1, 0) == 0)
        {
            try
            {
                ThreadPool.QueueUserWorkItem(static self => self.Drain(), this, preferLocal: false);
            }
            catch
            {
                Interlocked.Exchange(ref _draining, 0); // the next signal tries again
                throw;
            }
        }
    }

    // One pool thread at a time; a signal posted while the flag drops is picked up by the re-check.
    private void Drain()
    {
        do
        {
            try
            {
                while (_pending.TryDequeue(out var signal))
                {
                    Apply(signal);
                }
            }
            finally
            {
                // A full fence (not a release store): the emptiness check below must not move before it.
                Interlocked.Exchange(ref _draining, 0);
            }
        }
        while (!_pending.IsEmpty && Interlocked.CompareExchange(ref _draining, 1, 0) == 0);
    }

    private void Apply((long Id, bool Escape, long? ReleasedAt) signal)
    {
        try
        {
            if (signal.Escape)
            {
                overview.Escape(signal.Id);
            }
            else
            {
                overview.Release(signal.Id, signal.ReleasedAt);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Thread pool: an escaping exception would end the process.
            log.OverviewFailed(ex, "key_signal");
        }
    }
}
