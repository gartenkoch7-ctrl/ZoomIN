using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using ZoomIN.App.Lifecycle;
using ZoomIN.Platform.Diagnostics;
using ZoomIN.Watchdog;
using ZoomIN.Watchdog.Crashes;

namespace ZoomIN.App;

/// <summary>
/// Entry point. "ZoomIN.exe --watchdog &lt;pid&gt;" runs the watchdog without any UI (ADR-0007);
/// otherwise the WinUI app starts (same steps as the XAML-generated Main), once per session.
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

        using var instance = SingleInstance.TryAcquire();
        if (instance is null)
        {
            return 0;
        }

        Instance = instance;
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(callbackParams =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
        return 0;
    }
}
