using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZoomIN.App.Composition;
using ZoomIN.App.Lifecycle;
using ZoomIN.App.SecureDesktop.Checks;
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
/// the normal desktop, for quick tests; Strg+Alt+Umschalt+Z ends it, Strg+Alt+Umschalt+D saves a
/// debug snapshot (M10).
/// </summary>
internal sealed class SecureDesktopHost(TelemetryHost telemetry, IAppFolders folders)
{
    // Not the tray app's name: both run at the same time in the same session.
    private const string MutexName = @"Local\ZoomIN.SecureInstance";

    // The watch ends at its next poll after the cancel; the checks, if running, take well under a second.
    private static readonly TimeSpan FollowStopWait = TimeSpan.FromSeconds(3);

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
        using var exiting = new CancellationTokenSource();
        var start = telemetry.StartOperation("zoomin.secure.start");
        using (var services = ServiceRegistration.SecureDesktop(telemetry, folders).AddSecureChecks().BuildServiceProvider())
        {
            // The sanitizer has already cleared any export address: SYSTEM never sends (ADR-0019).
            telemetry.Apply(services.GetRequiredService<AppSettings>().Telemetry());
            log.Started(SessionUserFolders.IsSystem);
            var watchdog = services.GetRequiredService<IWatchdogLauncher>().Start(secure: true);
            var follow = Start(services, exit, log, start, watchdog, exiting.Token);
            exit.Wait();
            log.Exits();

            // The start watch uses the services: let it stop before they are disposed. CancelAsync, so
            // its last checks run on the pool, not inside this call where no time limit applies.
            _ = exiting.CancelAsync();
            try
            {
                follow.Wait(FollowStopWait);
            }
            catch (AggregateException ex)
            {
                log.ChecksFailed(ex.InnerException ?? ex);
            }
        }

        mutex.ReleaseMutex();
        return 0;
    }

    /// <param name="start">Ended when the engine runs, or with the first magnified image if the zoom starts on.</param>
    /// <returns>The start watch (<see cref="SecureStartWatch.FollowAsync"/>), running until the first image and the checks.</returns>
    private static Task Start(ServiceProvider services, ManualResetEventSlim exit, ILogger log, Activity? start, int? watchdog, CancellationToken exiting)
    {
        var processStart = ProcessStart();
        var controller = services.GetRequiredService<ZoomController>();
        var watch = services.GetRequiredService<SecureStartWatch>();

        // The loaded settings, not SettingsService.Current: Current reports the live model, which
        // always starts unzoomed, so the saved on/off state would be lost (observed 2026-11-03).
        var settings = services.GetRequiredService<AppSettings>();
        watch.Begin();
        controller.Initialize();
        if (settings.ZoomOn)
        {
            // The saved level at once, not a glide from 1× (M10, Q3: first image within 1 s).
            watch.SkipZoomAnimation();
            services.GetRequiredService<ZoomCommands>().Activate(settings.LastMode);
        }

        var hotkeys = services.GetRequiredService<IHotkeyService>();
        var exitShortcut = hotkeys.Register(HotkeyGesture.ExitSecureDesktop, exit.Set);
        if (!exitShortcut)
        {
            log.ExitShortcutUnavailable();
        }

        var snapshot = services.GetRequiredService<SecureSnapshotShortcut>();
        var snapshotShortcut = hotkeys.Register(HotkeyGesture.SecureDesktopSnapshot, snapshot.Take);
        if (!snapshotShortcut)
        {
            log.SnapshotShortcutUnavailable();
        }

        start?.SetTag("zoomin.secure.zoom_on", settings.ZoomOn);
        var running = (services.GetRequiredService<TimeProvider>().GetUtcNow() - processStart).TotalMilliseconds;
        log.EngineRunning(running, settings.ZoomOn);
        if (!settings.ZoomOn)
        {
            start?.Dispose();
        }

        var inputs = new SecureCheckInputs(watchdog, services.GetRequiredService<HotkeyBindings>().Unavailable, exitShortcut, snapshotShortcut);
        return Task.Run(() => watch.FollowAsync(processStart, settings.ZoomOn ? start : null, settings.ZoomOn, inputs, exiting), CancellationToken.None);
    }

    private static DateTimeOffset ProcessStart()
    {
        using var process = Process.GetCurrentProcess();
        return process.StartTime.ToUniversalTime();
    }
}
