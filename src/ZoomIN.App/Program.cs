using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using ZoomIN.Platform.Diagnostics;
using ZoomIN.Watchdog;
using ZoomIN.Watchdog.Crashes;

namespace ZoomIN.App;

/// <summary>
/// Entry point. "ZoomIN.exe --watchdog &lt;pid&gt;" runs the watchdog without any UI (ADR-0007);
/// otherwise the WinUI app starts (same steps as the XAML-generated Main).
/// </summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (WatchdogArguments.TryParse(args, out var processId))
        {
            return WatchdogRunner.Run(processId, Environment.ProcessPath!, new FileLog(), cleanup: [new AppBarCleanup()]);
        }

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
