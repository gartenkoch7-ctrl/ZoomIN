namespace ZoomIN.Core.Monitors;

/// <summary>
/// Which monitors are frozen (G2), part of <see cref="Zoom.ZoomModel"/> like its temporary levels.
/// Writers (the freeze controller) are serialised by a lock and publish a new <see cref="FreezeState"/>
/// with <see cref="Volatile"/>; render threads read <see cref="State"/> lock-free. Never saved: after
/// a restart nothing is frozen (Grilling 2026-11-11).
/// </summary>
public sealed class MonitorFreeze
{
    private readonly Lock _gate = new();
    private FreezeState _state = FreezeState.Empty;

    /// <summary>Raised after every change, on the thread that caused it (wakes the render threads).</summary>
    public event Action? Changed;

    public FreezeState State => Volatile.Read(ref _state);

    /// <summary>True if any monitor is frozen.</summary>
    public bool Any => State.Count > 0;

    /// <summary>The freeze of this monitor, or null; per frame, lock-free, 0 B.</summary>
    public FrozenMonitor? For(string monitorId) => State.For(monitorId);

    public bool IsFrozen(string monitorId) => For(monitorId) is not null;

    /// <summary>Freezes a monitor; a monitor frozen again gets the new freeze (a new view and still image).</summary>
    public void Freeze(FrozenMonitor monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        lock (_gate)
        {
            Volatile.Write(ref _state, _state.With(monitor));
        }

        Changed?.Invoke();
    }

    /// <summary>Unfreezes every monitor; returns the ones that were frozen.</summary>
    public IReadOnlyList<FrozenMonitor> UnfreezeAll() => Unfreeze(_ => true);

    /// <summary>
    /// Called under this lock with the monitors about to thaw and whether their level may come back,
    /// before the new state is published: the zoom model restores their targets first, so a render
    /// thread never sees a thawed monitor without its target (no dip to 1×).
    /// </summary>
    internal Action<IReadOnlyList<FrozenMonitor>, bool>? Thawing { get; set; }

    /// <summary>Unfreezes the monitors <paramref name="which"/> selects; returns them.</summary>
    /// <param name="restoreLevel">False if the monitor is gone or changed (display change): it starts at 1×.</param>
    public IReadOnlyList<FrozenMonitor> Unfreeze(Func<FrozenMonitor, bool> which, bool restoreLevel = true)
    {
        ArgumentNullException.ThrowIfNull(which);
        IReadOnlyList<FrozenMonitor> removed = [];
        try
        {
            lock (_gate)
            {
                var next = _state.Without(which, out removed);
                try
                {
                    if (removed.Count > 0)
                    {
                        Thawing?.Invoke(removed, restoreLevel);
                    }
                }
                finally
                {
                    // Published even if restoring the targets failed: a monitor must never stay frozen for good.
                    Volatile.Write(ref _state, next);
                }
            }
        }
        finally
        {
            // Listeners (zoom model, render wake-up) hear of every published thaw, also a failed restore.
            if (removed.Count > 0)
            {
                Changed?.Invoke();
            }
        }

        return removed;
    }
}
