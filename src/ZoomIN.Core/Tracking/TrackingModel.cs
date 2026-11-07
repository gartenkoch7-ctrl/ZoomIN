namespace ZoomIN.Core.Tracking;

/// <summary>
/// Shared focus/caret/notification state: written by the tracking thread, read lock-free by render threads.
/// </summary>
public sealed class TrackingModel
{
    private TrackingSnapshot _snapshot = TrackingSnapshot.Empty;
    private TrackingOptions _options;
    private long _notificationRequestedAt;

    public TrackingModel(TrackingOptions options)
    {
        _options = options;
    }

    /// <summary>Raised when the user changes options (not on every focus/caret move).</summary>
    public event Action? Changed;

    public TrackingSnapshot Snapshot => Volatile.Read(ref _snapshot);

    public TrackingOptions Options => Volatile.Read(ref _options);

    /// <summary>Stopwatch ticks of the last "go to the notification" hotkey; 0 = never.</summary>
    public long NotificationRequestedAt => Interlocked.Read(ref _notificationRequestedAt);

    public void Publish(TrackingSnapshot snapshot) => Volatile.Write(ref _snapshot, snapshot);

    public void SetOptions(TrackingOptions options)
    {
        Volatile.Write(ref _options, options);
        Changed?.Invoke();
    }

    /// <summary>Strg+Alt+Umschalt+N: pan to the visible notification, even if it is already in view.</summary>
    public void RequestNotification() =>
        Interlocked.Exchange(ref _notificationRequestedAt, System.Diagnostics.Stopwatch.GetTimestamp());

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
