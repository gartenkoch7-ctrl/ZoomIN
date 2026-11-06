using Microsoft.Extensions.DependencyInjection;
using ZoomIN.App.Settings;
using ZoomIN.App.Zoom;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Core.Zoom;
using ZoomIN.Platform.Automation;
using ZoomIN.Platform.Cursor;
using ZoomIN.Platform.Input;
using ZoomIN.Platform.Monitors;
using ZoomIN.Platform.Shell;
using ZoomIN.Platform.WindowFilters;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.App.Composition;

/// <summary>
/// The zoom engine without any WinUI part: models, input, tracking and rendering. Shared by the
/// tray app and the secure-desktop instance (ADR-0016), which differ only in log, settings stores
/// and what runs around the engine.
/// </summary>
internal static class EngineRegistration
{
    /// <summary>Expects telemetry (AddTelemetry), ISettingsStore and IProfileStore to be registered by the caller.</summary>
    public static IServiceCollection AddEngine(this IServiceCollection services)
    {
        // Settings and models
        services.AddSingleton(sp => sp.GetRequiredService<ISettingsStore>().Load());
        services.AddSingleton(sp => ZoomModelFactory.Create(sp.GetRequiredService<AppSettings>()));
        services.AddSingleton(sp => EffectsFactory.Create(sp.GetRequiredService<AppSettings>()));
        services.AddSingleton(sp => TrackingFactory.Create(sp.GetRequiredService<AppSettings>()));
        services.AddSingleton(sp => new WindowFilterModel(sp.GetRequiredService<AppSettings>().WindowFilter ?? WindowFilterOptions.Default));
        services.AddSingleton(sp => new RenderModels(
            sp.GetRequiredService<ZoomModel>(),
            sp.GetRequiredService<EffectsModel>(),
            sp.GetRequiredService<TrackingModel>(),
            sp.GetRequiredService<WindowFilterModel>(),
            new DebugSnapshots()));
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
        services.AddSingleton<IWindowFilterSource, Win32WindowFilterSource>();

        // Rendering
        services.AddSingleton<IZoomOutput, ZoomEngine>();
        services.AddSingleton<ZoomController>();
        return services;
    }
}
