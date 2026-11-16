using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Settings;

namespace ZoomIN.Core.Zoom;

/// <summary>Maps between persisted <see cref="AppSettings"/> and the runtime <see cref="ZoomModel"/> (M14 step 9).</summary>
public static class ZoomMapping
{
    public static ZoomModel Create(AppSettings settings)
    {
        var model = new ZoomModel(Levels(settings), settings.LastMode, settings.PerMonitorZoom, settings.AnimationMilliseconds / 1000.0);
        ApplyLayout(model, settings);
        return model;
    }

    /// <summary>Updates an existing model, e.g. after switching profiles.</summary>
    public static void ApplyTo(ZoomModel model, AppSettings settings)
    {
        model.ReplaceLevels(Levels(settings));
        model.SetMode(settings.LastMode);
        model.PerMonitor = settings.PerMonitorZoom;
        model.AnimationSeconds = settings.AnimationMilliseconds / 1000.0;
        ApplyLayout(model, settings);
    }

    /// <summary>
    /// What is saved as "zoom on" (secure desktop, next start): while frozen, a zoomed frozen monitor
    /// counts too (G2), although commands treat zoom as off while only it is zoomed. Win+L and exit lift
    /// the freeze first; what is saved then is the level the monitor thaws at.
    /// </summary>
    public static bool SavedOn(ZoomModel model) => model.IsOn || FrozenTargets.AnyFrozenZoomed(model.Frozen.State);

    public static AppSettings Apply(AppSettings settings, ZoomModel model) => model.Read(levels => settings with
    {
        ZoomOn = SavedOn(model),
        LastMode = model.Mode,
        PerMonitorZoom = model.PerMonitor,
        AnimationMilliseconds = (int)Math.Round(model.AnimationSeconds * 1000),
        LastZoomLevel = levels.Shared(ZoomMode.Fullscreen),
        MonitorZoomLevels = levels.PerMonitor(ZoomMode.Fullscreen),
        LensZoomLevel = levels.Shared(ZoomMode.Lens),
        LensMonitorZoomLevels = levels.PerMonitor(ZoomMode.Lens),
        DockedZoomLevel = levels.Shared(ZoomMode.Docked),
        DockedMonitorZoomLevels = levels.PerMonitor(ZoomMode.Docked),
        LensWidth = model.Lens.WidthFraction,
        LensHeight = model.Lens.HeightFraction,
        DockEdge = model.Dock.Edge,
        DockFraction = model.Dock.Fraction,
    });

    private static ZoomLevels Levels(AppSettings settings)
    {
        var levels = new ZoomLevels();
        levels.Load(ZoomMode.Fullscreen, settings.LastZoomLevel, settings.MonitorZoomLevels);
        levels.Load(ZoomMode.Lens, settings.LensZoomLevel, settings.LensMonitorZoomLevels);
        levels.Load(ZoomMode.Docked, settings.DockedZoomLevel, settings.DockedMonitorZoomLevels);
        return levels;
    }

    private static void ApplyLayout(ZoomModel model, AppSettings settings)
    {
        model.Lens = new LensSize(settings.LensWidth, settings.LensHeight);
        model.Dock = new DockLayout(settings.DockEdge, settings.DockFraction);
    }
}
