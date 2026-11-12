using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using ZoomIN.App.AgentDebug;
using ZoomIN.App.Composition;
using ZoomIN.App.Lifecycle;
using ZoomIN.App.SecureDesktop;
using ZoomIN.App.Updates;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Folders;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.AgentDebug;
using ZoomIN.Platform.Diagnostics;
using ZoomIN.Platform.Folders;
using ZoomIN.Platform.Lifecycle;
using ZoomIN.Platform.SecureDesktop;
using ZoomIN.Watchdog;
using ZoomIN.Watchdog.Crashes;

namespace ZoomIN.App;

/// <summary>
/// Entry point. "ZoomIN.exe --watchdog &lt;pid&gt;" runs the watchdog without any UI (ADR-0007),
/// "--apply-update …" the elevated install step (ADR-0015), "--mcp" the KI-Debug MCP server (ADR-0027); otherwise the WinUI app starts (same steps as the XAML-generated Main), once per session.
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

    /// <summary>How the tray app was started (ADR-0030); decides the Ease of Access signal in <see cref="App"/>.</summary>
    internal static LaunchOrigin Origin { get; private set; } = LaunchOrigin.Unknown;

    [STAThread]
    public static int Main(string[] args)
    {
        args = TraceParentArgument.Extract(args, out var traceParent);
        args = LaunchOrigin.ExtractMarker(args, out var leftJob);
        if (WatchdogArguments.TryParse(args, out var processId, out var secure))
        {
            return RunRole(TelemetryRole.Watchdog, traceParent, log =>
                new WatchdogRunner(Folders, log, [new AppBarCleanup(Folders), new SystemCursorCleanup(), new PointerClipCleanup()], new ProcessWatchdogSystem(), TimeProvider.System)
                    .Run(processId, Environment.ProcessPath!, secure));
        }

        // As SYSTEM, ZoomIN is always the secure-desktop instance, whatever the arguments: Windows
        // starts it that way on the sign-in screen, and SYSTEM must never get WinUI, dialogs or
        // the updater (ADR-0016).
        if (args is [SecureDesktopMode.Switch] || SessionUserFolders.IsSystem)
        {
            using var telemetry = Start(TelemetryRole.Secure, traceParent);
            return new SecureDesktopHost(telemetry, Folders).Run();
        }

        if (McpServerRole.IsRequested(args, Environment.ProcessPath))
        {
            // Started by an agent's MCP client; talks to the running ZoomIN over the pipe (ADR-0027).
            // ZoomIN.Mcp.exe never runs anything else: it has no UIAccess, the tray app would be half a ZoomIN.
            return RunRole(TelemetryRole.Mcp, traceParent, (telemetry, _) =>
                new McpServerRole(new PipeAgentClient(telemetry.Logger<PipeAgentClient>()), telemetry.Loggers).Run());
        }

        if (SecureDesktopSetting.TryParse(args, out var secureDesktopOn))
        {
            return RunFlow(TelemetryRole.Elevated, traceParent, services => services.GetRequiredService<ISecureDesktopSwitch>().ApplyElevated(secureDesktopOn));
        }

        if (UpdateCommand.TryParse(args, out var update))
        {
            return RunFlow(TelemetryRole.Update, traceParent, services => services.GetRequiredService<UpdateRunner>().Run(update));
        }

        if (args is [UninstallEntry.UninstallSwitch])
        {
            return RunFlow(TelemetryRole.Uninstall, traceParent, services => services.GetRequiredService<UninstallFlow>().Run());
        }

        if (args is [UninstallEntry.RemoveSwitch, var pid] && int.TryParse(pid, NumberStyles.None, CultureInfo.InvariantCulture, out var asker))
        {
            return RunFlow(TelemetryRole.Uninstall, traceParent, services => services.GetRequiredService<UninstallFlow>().RemoveElevated(asker));
        }

        var restarted = RestartArguments.TryParse(args, out var oldProcess);
        if (restarted)
        {
            WaitForExit(oldProcess);
        }

        // Ease of Access starts the tray app in a job that ends at the next lock screen (ADR-0030):
        // leave it before taking the single-instance mutex, so the new instance can take it.
        Origin = new LaunchOriginProbe().Read(leftJob) with { AfterRestart = restarted };
        if (Origin.ShouldLeaveJob && SingleInstance.IsTaken())
        {
            // Windows started its AT again while ZoomIN runs outside the job: nothing to do.
            return 0;
        }

        var early = Origin.ShouldLeaveJob ? Start(TelemetryRole.App, traceParent) : null;
        if (early is not null && LeaveJob(early, args))
        {
            early.Dispose();
            return 0;
        }

        using var instance = SingleInstance.TryAcquire(askToOpen: !Origin.ByEaseOfAccess);
        if (instance is null)
        {
            early?.Dispose();
            return 0;
        }

        Instance = instance;
        using var appTelemetry = early ?? Start(TelemetryRole.App, traceParent);
        Telemetry = appTelemetry;
        StartOperation = appTelemetry.StartOperation("zoomin.app.start");
        PrepareAutostart(appTelemetry, reported: early is not null);
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(callbackParams =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
        return 0;
    }

    /// <summary>Logs the job found and starts ZoomIN again outside it; true if this instance must exit.</summary>
    private static bool LeaveJob(TelemetryHost telemetry, string[] args)
    {
        using var operation = telemetry.StartOperation("zoomin.autostart.leave_job");
        var breakaway = new JobBreakaway(telemetry.Logger<JobBreakaway>());
        breakaway.Report(Origin);
        var left = breakaway.TryRelaunch(Environment.ProcessPath!, args);
        operation?.SetTag("zoomin.autostart.left_job", left);
        return left;
    }

    /// <summary>
    /// How Windows started the tray app (spike A1; unless <see cref="LeaveJob"/> logged it already)
    /// and the one-time move of the autostart into the Ease of Access list (ADR-0030), before any
    /// view model reads the autostart.
    /// </summary>
    private static void PrepareAutostart(TelemetryHost telemetry, bool reported)
    {
        if (!reported)
        {
            new JobBreakaway(telemetry.Logger<JobBreakaway>()).Report(Origin);
        }

        new AutostartMigration(new AutostartSelector(telemetry.Logger<AutostartSelector>()), telemetry.Logger<AutostartMigration>()).Migrate();
    }

    /// <summary>A short-lived role with its own small container (installation services, its flow).</summary>
    private static int RunFlow(TelemetryRole role, string? traceParent, Func<IServiceProvider, int> run) =>
        RunRole(role, traceParent, (telemetry, _) =>
        {
            using var services = ServiceRegistration.BuildRole(telemetry, Folders);
            return run(services);
        });

    private static int RunRole(TelemetryRole role, string? traceParent, Func<ILogger, int> run) =>
        RunRole(role, traceParent, (_, log) => run(log));

    /// <summary>A short-lived role: one operation from start to exit; a non-zero exit code marks it failed.</summary>
    private static int RunRole(TelemetryRole role, string? traceParent, Func<TelemetryHost, ILogger, int> run)
    {
        using var telemetry = Start(role, traceParent);
        using var operation = telemetry.StartOperation($"zoomin.{role.ToString().ToLowerInvariant()}.run");
        var exitCode = run(telemetry, telemetry.Loggers.CreateLogger("ZoomIN." + role));
        if (exitCode != 0)
        {
            operation?.SetStatus(ActivityStatusCode.Error, string.Create(CultureInfo.InvariantCulture, $"exit code {exitCode}"));
        }

        return exitCode;
    }

    // After an update: the old version must be gone before the single-instance check (ADR-0015).
    private static void WaitForExit(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.WaitForExit(TimeSpan.FromSeconds(15));
        }
        catch (ArgumentException)
        {
            // Already gone.
        }
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
