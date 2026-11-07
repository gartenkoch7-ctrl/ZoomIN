using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using ZoomIN.App.Lifecycle;
using ZoomIN.App.SecureDesktop;
using ZoomIN.App.Updates;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Folders;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Diagnostics;
using ZoomIN.Platform.Folders;
using ZoomIN.Platform.SecureDesktop;
using ZoomIN.Watchdog;
using ZoomIN.Watchdog.Crashes;

namespace ZoomIN.App;

/// <summary>
/// Entry point. "ZoomIN.exe --watchdog &lt;pid&gt;" runs the watchdog without any UI (ADR-0007),
/// "--apply-update …" the elevated install step (ADR-0015); otherwise the WinUI app starts (same steps as the XAML-generated Main), once per session.
/// Every role starts telemetry the same way (ADR-0019); "--traceparent" is taken out first.
/// </summary>
public static class Program
{
    internal static SingleInstance? Instance { get; private set; }

    /// <summary>The ZoomIN folders of this account, for every role and the DI container (ADR-0025).</summary>
    internal static IAppFolders Folders { get; } = new WindowsAppFolders();

    /// <summary>Telemetry of the tray app; set before the WinUI app starts.</summary>
    internal static TelemetryHost? Telemetry { get; private set; }

    /// <summary>From process start until the tray app is ready (ended by <see cref="App"/>).</summary>
    internal static Activity? StartOperation { get; set; }

    [STAThread]
    public static int Main(string[] args)
    {
        args = TraceParentArgument.Extract(args, out var traceParent);
        if (WatchdogArguments.TryParse(args, out var processId, out var secure))
        {
            return RunRole(TelemetryRole.Watchdog, traceParent, log =>
                WatchdogRunner.Run(processId, Environment.ProcessPath!, log, cleanup: [new AppBarCleanup(Folders), new SystemCursorCleanup()], secure));
        }

        // As SYSTEM, ZoomIN is always the secure-desktop instance, whatever the arguments: Windows
        // starts it that way on the sign-in screen, and SYSTEM must never get WinUI, dialogs or
        // the updater (ADR-0016).
        if (args is [SecureDesktopMode.Switch] || SessionUserFolders.IsSystem)
        {
            using var telemetry = Start(TelemetryRole.Secure, traceParent);
            return SecureDesktopHost.Run(telemetry);
        }

        if (SecureDesktopSetting.TryParse(args, out var secureDesktopOn))
        {
            return RunRole(TelemetryRole.Elevated, traceParent, log => SecureDesktopSetting.RunElevated(secureDesktopOn, log));
        }

        if (UpdateCommand.TryParse(args, out var update))
        {
            return RunRole(TelemetryRole.Update, traceParent, log => UpdateRunner.Run(update, log));
        }

        if (args is [UninstallEntry.UninstallSwitch])
        {
            return RunRole(TelemetryRole.Uninstall, traceParent, UninstallFlow.Run);
        }

        if (args is [UninstallEntry.RemoveSwitch, var pid] && int.TryParse(pid, NumberStyles.None, CultureInfo.InvariantCulture, out var asker))
        {
            return RunRole(TelemetryRole.Uninstall, traceParent, log => UninstallFlow.RemoveElevated(asker, log));
        }

        RestartArguments.WaitIfRequested(args);
        using var instance = SingleInstance.TryAcquire();
        if (instance is null)
        {
            return 0;
        }

        Instance = instance;
        using var appTelemetry = Start(TelemetryRole.App, traceParent);
        Telemetry = appTelemetry;
        StartOperation = appTelemetry.StartOperation("zoomin.app.start");
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(callbackParams =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
        return 0;
    }

    /// <summary>A short-lived role: one operation from start to exit; a non-zero exit code marks it failed.</summary>
    private static int RunRole(TelemetryRole role, string? traceParent, Func<ILogger, int> run)
    {
        using var telemetry = Start(role, traceParent);
        using var operation = telemetry.StartOperation($"zoomin.{role.ToString().ToLowerInvariant()}.run");
        var exitCode = run(telemetry.Loggers.CreateLogger("ZoomIN." + role));
        if (exitCode != 0)
        {
            operation?.SetStatus(ActivityStatusCode.Error, string.Create(CultureInfo.InvariantCulture, $"exit code {exitCode}"));
        }

        return exitCode;
    }

    private static TelemetryHost Start(TelemetryRole role, string? traceParent)
    {
        var telemetry = TelemetryHost.Start(role, Folders, traceParent);
        Core.Diagnostics.Telemetry.OnFlush(telemetry.Flush);
        LogBackgroundFailures(telemetry);
        return telemetry;
    }

    /// <summary>
    /// Errors on background threads end the process (and the watchdog restarts it); log them first
    /// and write the buffer, so every crash can be analysed (M8). UI-thread errors are logged in <see cref="App"/>.
    /// </summary>
    private static void LogBackgroundFailures(TelemetryHost telemetry)
    {
        var log = telemetry.Logger<App>();
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            log.UnhandledException(e.ExceptionObject as Exception);
            telemetry.Flush(TimeSpan.FromSeconds(1));
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            log.UnobservedTaskException(e.Exception);
            e.SetObserved();
        };
    }
}
