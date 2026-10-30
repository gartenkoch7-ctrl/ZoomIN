namespace ZoomIN.App.Lifecycle;

/// <summary>
/// Only one ZoomIN per user session (M7). A second start asks the running instance to open its
/// settings and exits. The watchdog mode does not take part.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\ZoomIN.Instance";
    private const string OpenEventName = @"Local\ZoomIN.OpenSettings";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _openRequested;
    private RegisteredWaitHandle? _registration;

    private SingleInstance(Mutex mutex)
    {
        _mutex = mutex;
        _openRequested = new EventWaitHandle(false, EventResetMode.AutoReset, OpenEventName);
    }

    /// <returns>Null if another instance runs; it has then been asked to open its settings.</returns>
    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(false, MutexName);
        try
        {
            if (!mutex.WaitOne(0))
            {
                mutex.Dispose();
                using var open = new EventWaitHandle(false, EventResetMode.AutoReset, OpenEventName);
                open.Set();
                return null;
            }
        }
        catch (AbandonedMutexException)
        {
            // The previous instance crashed while holding the mutex; it is ours now.
        }

        return new SingleInstance(mutex);
    }

    /// <summary>Calls <paramref name="onOpen"/> (on a thread-pool thread) whenever another start asks for it.</summary>
    public void ListenForOpenRequests(Action onOpen) =>
        _registration = ThreadPool.RegisterWaitForSingleObject(_openRequested, (_, _) => onOpen(), null, Timeout.Infinite, executeOnlyOnce: false);

    public void Dispose()
    {
        _registration?.Unregister(null);
        _openRequested.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
