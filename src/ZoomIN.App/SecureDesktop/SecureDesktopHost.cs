using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ZoomIN.App.Composition;
using ZoomIN.App.Settings;
using ZoomIN.App.Zoom;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Platform.SecureDesktop;

namespace ZoomIN.App.SecureDesktop;

/// <summary>
/// "ZoomIN.exe --secure-desktop" (ADR-0016): the zoom engine without WinUI, tray, settings window,
/// updates or links, for the sign-in screen, lock screen, Ctrl+Alt+Del and UAC. Windows starts it
/// and ends it on every desktop switch. Started by hand as a normal user it runs the same way on
/// the normal desktop, for quick tests; Strg+Alt+Umschalt+Z ends it.
/// </summary>
internal static class SecureDesktopHost
{
    public const string Switch = "--secure-desktop";

    // Not the tray app's name: both run at the same time in the same session.
    private const string MutexName = @"Local\ZoomIN.SecureInstance";

    public static int Run()
    {
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
        }

        if (!services.GetRequiredService<IHotkeyService>().Register(HotkeyGesture.ExitSecureDesktop, exit.Set))
        {
            log.Warn("Exit shortcut for the secure desktop is unavailable");
        }

        var elapsed = DateTime.Now - Process.GetCurrentProcess().StartTime;
        log.Info($"Secure desktop engine running after {elapsed.TotalMilliseconds:0} ms (zoom on: {settings.ZoomOn})");
    }
}
