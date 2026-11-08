using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZoomIN.App.Composition;
using ZoomIN.App.Lifecycle;
using ZoomIN.App.Zoom;
using ZoomIN.Core.Folders;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Settings;
using ZoomIN.Platform.Diagnostics;
using ZoomIN.Platform.SecureDesktop;
using ZoomIN.Watchdog.Crashes;

namespace ZoomIN.App.SecureDesktop;

/// <summary>
/// "ZoomIN.exe --secure-desktop" (ADR-0016): the zoom engine without WinUI, tray, settings window,
/// updates or links, for the sign-in screen, lock screen, Ctrl+Alt+Del and UAC. Windows starts it
/// and ends it on every desktop switch. Started by hand as a normal user it runs the same way on
/// the normal desktop, for quick tests; Strg+Alt+Umschalt+Z ends it.
/// </summary>
internal sealed class SecureDesktopHost(TelemetryHost telemetry, IAppFolders folders)
{
    // Not the tray app's name: both run at the same time in the same session.
    private const string MutexName = @"Local\ZoomIN.SecureInstance";

    private static readonly TimeSpan FirstFrameTimeout = TimeSpan.FromSeconds(10);

    public int Run()
    {
        var log = telemetry.Logger<SecureDesktopHost>();
        if (new SecureDesktopGiveUp(folders).IsActive())
        {
            log.GaveUp();
            return 0;
        }

        using var mutex = new Mutex(false, MutexName);
        try
        {
            if (!mutex.WaitOne(0))
            {
                return 0;
            }
        }
        catch (AbandonedMutexException)
        {
            // The previous secure instance was ended while holding it; it is ours now.
        }

        using var exit = new ManualResetEventSlim();
        var start = telemetry.StartOperation("zoomin.secure.start");
        using (var services = ServiceRegistration.BuildSecureDesktop(telemetry, folders))
        {
            // The sanitizer has already cleared any export address: SYSTEM never sends (ADR-0019).
            telemetry.Apply(services.GetRequiredService<AppSettings>().Telemetry());
            log.Started(SessionUserFolders.IsSystem);
            services.GetRequiredService<IWatchdogLauncher>().Start(secure: true);
            Start(services, exit, log, start);
            exit.Wait();
            log.Exits();
        }

        mutex.ReleaseMutex();
        return 0;
    }

    /// <param name="start">Ended when the engine runs, or with the first magnified image if the zoom starts on.</param>
    private static void Start(ServiceProvider services, ManualResetEventSlim exit, ILogger log, Activity? start)
    {
        var controller = services.GetRequiredService<ZoomController>();
        // The loaded settings, not SettingsService.Current: Current reports the live model, which
        // always starts unzoomed, so the saved on/off state would be lost (observed 2026-11-03).
        var settings = services.GetRequiredService<AppSettings>();
        controller.Initialize();
        if (settings.ZoomOn)
        {
            services.GetRequiredService<ZoomCommands>().Activate(settings.LastMode);
            _ = Task.Run(() => LogFirstFrame(controller, log, start));
        }

        if (!services.GetRequiredService<IHotkeyService>().Register(HotkeyGesture.ExitSecureDesktop, exit.Set))
        {
            log.ExitShortcutUnavailable();
        }

        start?.SetTag("zoomin.secure.zoom_on", settings.ZoomOn);
        var running = SinceStart().TotalMilliseconds;
        log.EngineRunning(running, settings.ZoomOn);
        if (!settings.ZoomOn)
        {
            start?.Dispose();
        }
    }

    /// <summary>Acceptance criterion of M10: the magnified image within 1 s of the switch.</summary>
    private static async Task LogFirstFrame(ZoomController controller, ILogger log, Activity? start)
    {
        using (start)
        {
            var deadline = Stopwatch.StartNew();
            while (!controller.Status.IsVisible)
            {
                if (deadline.Elapsed > FirstFrameTimeout)
                {
                    log.NoFirstImage(FirstFrameTimeout.TotalSeconds);
                    start?.SetStatus(ActivityStatusCode.Error, "no magnified image");
                    return;
                }

                await Task.Delay(10).ConfigureAwait(false);
            }

            var firstImage = SinceStart().TotalMilliseconds;
            log.FirstImage(firstImage);
        }
    }

    private static TimeSpan SinceStart()
    {
        using var process = Process.GetCurrentProcess();
        return DateTime.Now - process.StartTime;
    }
}
