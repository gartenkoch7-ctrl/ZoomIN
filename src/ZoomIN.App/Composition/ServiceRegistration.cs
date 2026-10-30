using Microsoft.Extensions.DependencyInjection;
using ZoomIN.App.Views;
using ZoomIN.App.Zoom;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Zoom;
using ZoomIN.Platform.Diagnostics;
using ZoomIN.Platform.Input;
using ZoomIN.Platform.Monitors;
using ZoomIN.Platform.Settings;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.App.Composition;

/// <summary>The only place where concrete Platform/Rendering types are wired to Core interfaces.</summary>
internal static class ServiceRegistration
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ILog, FileLog>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton(sp => sp.GetRequiredService<ISettingsStore>().Load());
        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<AppSettings>();
            return new ZoomModel(
                settings.LastZoomLevel,
                settings.MonitorZoomLevels,
                settings.PerMonitorZoom,
                settings.AnimationMilliseconds / 1000.0);
        });

        services.AddSingleton<ICursorSource, Win32CursorSource>();
        services.AddSingleton<IMonitorProvider, Win32MonitorProvider>();
        services.AddSingleton<IHotkeyService, Win32HotkeyService>();
        services.AddSingleton<IMouseWheelHook, Win32WheelHook>();
        services.AddSingleton<IZoomOutput, ZoomEngine>();
        services.AddSingleton<ZoomController>();
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }
}
