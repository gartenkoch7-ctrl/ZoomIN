namespace ZoomIN.Core.Tracking;

/// <summary>
/// Shared focus/caret state: written by the tracking thread, read lock-free by render threads.
/// </summary>
public sealed class TrackingModel
{
    private TrackingSnapshot _snapshot = TrackingSnapshot.Empty;
    private TrackingOptions _options;

    public TrackingModel(TrackingOptions options)
    {
        _options = options;
    }

    /// <summary>Raised when the user changes options (not on every focus/caret move).</summary>
    public event Action? Changed;

    public TrackingSnapshot Snapshot => Volatile.Read(ref _snapshot);

    public TrackingOptions Options => Volatile.Read(ref _options);

    public void Publish(TrackingSnapshot snapshot) => Volatile.Write(ref _snapshot, snapshot);

    /// <summary>Strg+Alt+T.</summary>
    public void ToggleEnabled()
    {
        TrackingOptions current, next;
        do
        {
            current = Volatile.Read(ref _options);
            next = current with { Enabled = !current.Enabled };
        }
        while (Interlocked.CompareExchange(ref _options, next, current) != current);

        Changed?.Invoke();
    }
}
