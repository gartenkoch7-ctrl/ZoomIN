using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZoomIN.App.Islands;
using ZoomIN.App.Freeze;
using ZoomIN.App.Localization;
using ZoomIN.App.Markers;
using ZoomIN.App.Overview;
using ZoomIN.App.Settings;
using ZoomIN.App.WindowDrag;
using ZoomIN.App.Zoom;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Localization;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Shell;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.WindowDrag;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Core.Zoom;
using ZoomIN.Platform.Automation;
using ZoomIN.Platform.Cursor;
using ZoomIN.Platform.Input;
using ZoomIN.Platform.Monitors;
using ZoomIN.Platform.PointerFence;
using ZoomIN.Platform.Shell;
using ZoomIN.Platform.WindowDrag;
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
        services.AddSingleton<SettingsLoader>();
        services.AddSingleton(sp => sp.GetRequiredService<SettingsLoader>().Load());
        services.AddSingleton(sp => ZoomMapping.Create(sp.GetRequiredService<AppSettings>()));
        services.AddSingleton(sp => EffectsMapping.Create(sp.GetRequiredService<AppSettings>()));
        services.AddSingleton(sp => TrackingMapping.Create(sp.GetRequiredService<AppSettings>()));
        services.AddSingleton(sp => WindowDragMapping.Create(sp.GetRequiredService<AppSettings>(), sp.GetRequiredService<ZoomModel>()));
        services.AddSingleton(sp => new WindowFilterModel(sp.GetRequiredService<AppSettings>().WindowFilter ?? WindowFilterOptions.Default));
        services.AddSingleton(sp => new RenderModels(
            sp.GetRequiredService<ZoomModel>(),
            sp.GetRequiredService<EffectsModel>(),
            sp.GetRequiredService<TrackingModel>(),
            sp.GetRequiredService<WindowFilterModel>(),
            sp.GetRequiredService<DebugSnapshots>(),
            sp.GetRequiredService<SmartInvertHealth>(),
            sp.GetRequiredService<OverviewModel>(),
            sp.GetRequiredService<ViewCenterRequests>()) { Islands = sp.GetRequiredService<IslandModel>() });
        services.AddSingleton(sp => IslandMapping.Create(sp.GetRequiredService<AppSettings>()));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(sp => new OverviewModel(sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<ViewCenterRequests>();
        services.AddSingleton<DebugSnapshots>();
        services.AddSingleton<SmartInvertHealth>();
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
        services.AddSingleton<OverviewKeyWatch>();
        services.AddSingleton<IOverviewKeys>(sp => sp.GetRequiredService<OverviewKeyWatch>());
        services.AddSingleton<IslandEditCapture>();
        services.AddSingleton<IIslandEditInput>(sp => sp.GetRequiredService<IslandEditCapture>());
        services.AddSingleton<IAnnouncer, UiaAnnouncer>();

        // The tray app registers the shared localizer itself. The secure instance only needs one to build
        // OverviewController; the overview has no shortcut there (SecureSettingsSanitizer).
        services.TryAddSingleton<ILocalizer>(_ => AppTexts.CreateLocalizer());
        services.AddSingleton<IHoldKeySource, Win32HoldKeyHook>();
        services.AddSingleton<MarkerInput>();
        services.AddSingleton<ITrackingSource, UiaTrackingSource>();
        services.AddSingleton<IWindowFilterSource, Win32WindowFilterSource>();
        services.AddSingleton<IWindowDragSource, Win32WindowDragSource>();

        // Pointer fence and moves on command (ADR-0028, M19): idle until a lane forbids an area or adds a filter.
        services.AddSingleton<IPointerFence, Win32PointerFence>();
        services.AddSingleton<IPointerMover, Win32PointerMover>();

        // Rendering
        services.AddSingleton<IZoomOutput, ZoomEngine>();
        // Commands and the engine start (M14 step 10: split out of ZoomController).
        services.AddSingleton<ZoomCommands>();
        services.AddSingleton<WindowFilterCommands>();
        services.AddSingleton<HotkeyBindings>();
        services.AddSingleton<InputSources>();
        services.AddSingleton<DockedStripSync>();
        services.AddSingleton<OverviewController>();
        services.AddSingleton<OverviewDebug>();
        services.AddSingleton<IslandController>();
        services.AddSingleton<IslandVisibility>();
        services.AddSingleton<WindowDragSync>();
        services.AddSingleton<MonitorSwitchController>();
        services.AddSingleton<MonitorFreezeController>();
        services.AddSingleton<ZoomController>();
        return services;
    }
}
