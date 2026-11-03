using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using ZoomIN.App.Lifecycle;
using ZoomIN.App.Updates;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Diagnostics;
using ZoomIN.Watchdog;
using ZoomIN.Watchdog.Crashes;

namespace ZoomIN.App;

/// <summary>
/// Entry point. "ZoomIN.exe --watchdog &lt;pid&gt;" runs the watchdog without any UI (ADR-0007),
/// "--apply-update …" the elevated install step (ADR-0015); otherwise the WinUI app starts (same steps as the XAML-generated Main), once per session.
/// </summary>
public static class Program
{
    internal static SingleInstance? Instance { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        if (WatchdogArguments.TryParse(args, out var processId))
        {
            return WatchdogRunner.Run(processId, Environment.ProcessPath!, new FileLog(), cleanup: [new AppBarCleanup(), new SystemCursorCleanup()]);
        }

        if (UpdateCommand.TryParse(args, out var update))
        {
            return UpdateRunner.Run(update, new FileLog());
        }

        RestartArguments.WaitIfRequested(args);
        using var instance = SingleInstance.TryAcquire();
        if (instance is null)
        {
            return 0;
        }

        Instance = instance;
        LogBackgroundFailures(new FileLog());
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(callbackParams =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
        return 0;
    }

    /// <summary>
    /// Errors on background threads end the process (and the watchdog restarts it); log them first
    /// so every crash can be analysed (M8). UI-thread errors are logged in <see cref="App"/>.
    /// </summary>
    private static void LogBackgroundFailures(FileLog log)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            log.Error("Unhandled exception, ZoomIN ends", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            log.Error("Unobserved task exception", e.Exception);
            e.SetObserved();
        };
    }
}
