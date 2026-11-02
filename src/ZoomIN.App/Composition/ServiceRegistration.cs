using Microsoft.Extensions.DependencyInjection;
using ZoomIN.App.Lifecycle;
using ZoomIN.App.Settings;
using ZoomIN.App.Zoom;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Zoom;
using ZoomIN.Platform.Automation;
using ZoomIN.Platform.Cursor;
using ZoomIN.Platform.Diagnostics;
using ZoomIN.Platform.Input;
using ZoomIN.Platform.Lifecycle;
using ZoomIN.Platform.Monitors;
using ZoomIN.Platform.Settings;
using ZoomIN.Platform.Shell;
using ZoomIN.Platform.Updates;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.App.Composition;

/// <summary>The only place where concrete Platform/Rendering types are wired to Core interfaces.</summary>
internal static class ServiceRegistration
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        // Settings and models
        services.AddSingleton<ILog, FileLog>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IProfileStore, JsonProfileStore>();
        services.AddSingleton(sp => sp.GetRequiredService<ISettingsStore>().Load());
        services.AddSingleton(sp => ZoomModelFactory.Create(sp.GetRequiredService<AppSettings>()));
        services.AddSingleton(sp => EffectsFactory.Create(sp.GetRequiredService<AppSettings>()));
        services.AddSingleton(sp => TrackingFactory.Create(sp.GetRequiredService<AppSettings>()));
        services.AddSingleton(sp => new RenderModels(
            sp.GetRequiredService<ZoomModel>(),
            sp.GetRequiredService<EffectsModel>(),
            sp.GetRequiredService<TrackingModel>()));
        services.AddSingleton<SettingsService>();
        services.AddSingleton<ProfileService>();

        // Platform
        services.AddSingleton<ICursorSource, Win32CursorSource>();
        services.AddSingleton<ICursorShapeSource, Win32CursorShapeSource>();
        services.AddSingleton<ISystemCursor, MagnifierSystemCursor>();
        services.AddSingleton(sp => new PointerServices(
            sp.GetRequiredService<ICursorSource>(),
            sp.GetRequiredService<ICursorShapeSource>(),
            sp.GetRequiredService<ISystemCursor>()));
        services.AddSingleton<IMonitorProvider, Win32MonitorProvider>();
        services.AddSingleton<IScreenReservation, AppBarReservation>();
        services.AddSingleton<IHotkeyService, Win32HotkeyService>();
        services.AddSingleton<IMouseWheelHook, Win32WheelHook>();
        services.AddSingleton<IInputActivitySource, Win32InputActivityHook>();
        services.AddSingleton<ITrackingSource, UiaTrackingSource>();
        services.AddSingleton<ITrayIcon, Win32TrayIcon>();
        services.AddSingleton<IAutostart, RegistryAutostart>();
        services.AddSingleton<IUpdateChecker, GitHubUpdateChecker>();

        // Rendering and app
        services.AddSingleton<IZoomOutput, ZoomEngine>();
        services.AddSingleton<ZoomController>();
        services.AddSingleton<AppShell>();

        return services.BuildServiceProvider();
    }
}
