using Microsoft.Extensions.Logging;
using ZoomIN.App.Markers;
using ZoomIN.App.Pointer;
using ZoomIN.App.SecureDesktop;
using ZoomIN.App.Settings;
using ZoomIN.App.WindowDrag;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Zoom;

/// <summary>
/// Starts the engine: shortcuts, mouse wheel and activity hooks, zoom output, focus tracking, the
/// filter per window, the docked strip reservation, zoom while moving windows (G1) and the hold key of the highlights (E1). The
/// commands themselves are in <see cref="ZoomCommands"/>, <see cref="WindowFilterCommands"/> and <see cref="HotkeyBindings"/>.
/// Saving is done by <see cref="SettingsService"/>.
/// </summary>
public sealed class ZoomController(
    SettingsService settings, HotkeyBindings hotkeys, ZoomCommands zoom, IZoomOutput output, InputSources inputs,
    DockedStripSync dockedStrip, WindowDragSync windowDrag, MarkerInput markers,
    ILogger<ZoomController> log, EngineScope scope) : IDisposable
{
    public ZoomStatus Status => output.Status;

    public void Initialize()
    {
        var models = settings.Models;
        if (scope == EngineScope.SecureDesktop)
        {
            log.CoreEngine(scope);
        }

        hotkeys.Register();
        inputs.Wheel.Start(settings.Current.WheelModifiers, zoom.ZoomBy);
        inputs.Activity.Start(new PointerActivity(models.Effects, models.Markers));
        markers.Start();
        models.Zoom.Changed += dockedStrip.Update;
        output.Start();
        inputs.Tracking.Start(models.Tracking);
        inputs.WindowFilter.Start(models.Windows);

        // Zoom while dragging windows is not part of the core engine on the secure desktop.
        if (scope == EngineScope.Full)
        {
            windowDrag.Start();
        }
    }

    public void Dispose()
    {
        settings.Models.Zoom.Changed -= dockedStrip.Update;
        markers.Dispose();
    }
}
