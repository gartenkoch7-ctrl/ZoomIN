using ZoomIN.Core.Modes;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Zoom;

/// <summary>Maps between persisted <see cref="AppSettings"/> and the runtime <see cref="ZoomModel"/>.</summary>
internal static class ZoomModelFactory
{
    public static ZoomModel Create(AppSettings settings)
    {
        var levels = new ZoomLevels();
        levels.Load(ZoomMode.Fullscreen, settings.LastZoomLevel, settings.MonitorZoomLevels);
        levels.Load(ZoomMode.Lens, settings.LensZoomLevel, settings.LensMonitorZoomLevels);
        levels.Load(ZoomMode.Docked, settings.DockedZoomLevel, settings.DockedMonitorZoomLevels);

        return new ZoomModel(levels, settings.LastMode, settings.PerMonitorZoom, settings.AnimationMilliseconds / 1000.0)
        {
            // Resize(0, 0) clamps hand-edited values into the allowed range.
            Lens = new LensSize(settings.LensWidth, settings.LensHeight).Resize(0, 0),
            Dock = new DockLayout(settings.DockEdge, settings.DockFraction),
        };
    }

    public static AppSettings Apply(AppSettings settings, ZoomModel model) => model.Read(levels => settings with
    {
        LastMode = model.Mode,
        PerMonitorZoom = model.PerMonitor,
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
}
