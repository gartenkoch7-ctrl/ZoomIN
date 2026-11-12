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

    /// <summary>Unfreezes the monitors <paramref name="which"/> selects; returns them.</summary>
    public IReadOnlyList<FrozenMonitor> Unfreeze(Func<FrozenMonitor, bool> which)
    {
        ArgumentNullException.ThrowIfNull(which);
        IReadOnlyList<FrozenMonitor> removed;
        lock (_gate)
        {
            Volatile.Write(ref _state, _state.Without(which, out removed));
        }

        if (removed.Count > 0)
        {
            Changed?.Invoke();
        }

        return removed;
    }
}
