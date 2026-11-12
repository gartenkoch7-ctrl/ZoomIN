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

    /// <param name="askToOpen">
    /// False for a start Windows made by itself (Ease of Access, ADR-0030): the user did not ask
    /// for a window, so a running instance is left alone.
    /// </param>
    /// <returns>Null if another instance runs; it has then been asked to open its settings.</returns>
    public static SingleInstance? TryAcquire(bool askToOpen = true)
    {
        var mutex = new Mutex(false, MutexName);
        try
        {
            if (!mutex.WaitOne(0))
            {
                mutex.Dispose();
                if (askToOpen)
                {
                    using var open = new EventWaitHandle(false, EventResetMode.AutoReset, OpenEventName);
                    open.Set();
                }

                return null;
            }
        }
        catch (AbandonedMutexException)
        {
            // The previous instance crashed while holding the mutex; it is ours now.
        }

        return new SingleInstance(mutex);
    }

    /// <summary>Another ZoomIN runs; the mutex is held only for the check itself.</summary>
    public static bool IsTaken()
    {
        if (!Mutex.TryOpenExisting(MutexName, out var mutex))
        {
            return false;
        }

        using (mutex)
        {
            try
            {
                if (!mutex.WaitOne(0))
                {
                    return true;
                }
            }
            catch (AbandonedMutexException)
            {
                // The previous instance crashed: ours now, so nobody runs.
            }

            mutex.ReleaseMutex();
            return false;
        }
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
