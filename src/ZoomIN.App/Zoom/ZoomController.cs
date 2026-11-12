using ZoomIN.App.Markers;
using ZoomIN.App.Pointer;
using ZoomIN.App.Settings;
using ZoomIN.App.WindowDrag;
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
    DockedStripSync dockedStrip, WindowDragSync windowDrag, MarkerInput markers) : IDisposable
{
    public ZoomStatus Status => output.Status;

    public void Initialize()
    {
        var models = settings.Models;
        hotkeys.Register();
        inputs.Wheel.Start(settings.Current.WheelModifiers, zoom.ZoomBy);
        inputs.Activity.Start(new PointerActivity(models.Effects, models.Markers));
        markers.Start();
        models.Zoom.Changed += dockedStrip.Update;
        output.Start();
        inputs.Tracking.Start(models.Tracking);
        inputs.WindowFilter.Start(models.Windows);
        windowDrag.Start();
    }

    public void Dispose()
    {
        settings.Models.Zoom.Changed -= dockedStrip.Update;
        markers.Dispose();
    }
}
