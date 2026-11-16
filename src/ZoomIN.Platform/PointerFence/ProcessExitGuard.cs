namespace ZoomIN.Platform.PointerFence;

/// <summary>
/// Runs <paramref name="release"/> on process exit and on an unhandled exception: a crash or
/// Environment.Exit skips Dispose, and a ClipCursor rectangle would outlive the process (the hook
/// goes with it). Environment.FailFast skips even these handlers; the watchdog covers that.
/// </summary>
internal sealed class ProcessExitGuard : IDisposable
{
    private readonly Action _release;

    public ProcessExitGuard(Action release)
    {
        _release = release;
        AppDomain.CurrentDomain.ProcessExit += OnExit;
        AppDomain.CurrentDomain.UnhandledException += OnExit;
    }

    public void Dispose()
    {
        AppDomain.CurrentDomain.ProcessExit -= OnExit;
        AppDomain.CurrentDomain.UnhandledException -= OnExit;
    }

    private void OnExit(object? sender, EventArgs e) => _release();
}
