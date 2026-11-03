using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ZoomIN.App.Composition;
using ZoomIN.App.Lifecycle;
using ZoomIN.App.Settings;
using ZoomIN.App.Zoom;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Hotkeys;
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
internal static class SecureDesktopHost
{
    // Not the tray app's name: both run at the same time in the same session.
    private const string MutexName = @"Local\ZoomIN.SecureInstance";

    private static readonly TimeSpan FirstFrameTimeout = TimeSpan.FromSeconds(10);

    public static int Run()
    {
        if (SecureDesktopGiveUp.IsActive())
        {
            new FileLog().Warn("Secure desktop: off until Windows restarts (watchdog gave up after repeated crashes)");
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
        using (var services = ServiceRegistration.BuildSecureDesktop())
        {
            var log = services.GetRequiredService<ILog>();
            log.Info($"ZoomIN secure desktop started (SYSTEM: {SessionUserFolders.IsSystem})");
            WatchdogStarter.Start(log, secure: true);
            Start(services, exit, log);
            exit.Wait();
            log.Info("ZoomIN secure desktop exits");
        }

        mutex.ReleaseMutex();
        return 0;
    }

    private static void Start(ServiceProvider services, ManualResetEventSlim exit, ILog log)
    {
        var controller = services.GetRequiredService<ZoomController>();
        var settings = services.GetRequiredService<SettingsService>().Current;
        controller.Initialize();
        if (settings.ZoomOn)
        {
            controller.Activate(settings.LastMode);
            _ = Task.Run(() => LogFirstFrame(controller, log));
        }

        if (!services.GetRequiredService<IHotkeyService>().Register(HotkeyGesture.ExitSecureDesktop, exit.Set))
        {
            log.Warn("Exit shortcut for the secure desktop is unavailable");
        }

        log.Info($"Secure desktop engine running after {SinceStart().TotalMilliseconds:0} ms (zoom on: {settings.ZoomOn})");
    }

    /// <summary>Acceptance criterion of M10: the magnified image within 1 s of the switch.</summary>
    private static async Task LogFirstFrame(ZoomController controller, ILog log)
    {
        var deadline = Stopwatch.StartNew();
        while (!controller.Status.IsVisible)
        {
            if (deadline.Elapsed > FirstFrameTimeout)
            {
                log.Warn($"Secure desktop: no magnified image after {FirstFrameTimeout.TotalSeconds:0} s");
                return;
            }

            await Task.Delay(10).ConfigureAwait(false);
        }

        log.Info($"Secure desktop: first magnified image after {SinceStart().TotalMilliseconds:0} ms");
    }

    private static TimeSpan SinceStart() => DateTime.Now - Process.GetCurrentProcess().StartTime;
}
