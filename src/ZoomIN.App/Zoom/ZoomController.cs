using ZoomIN.App.Pointer;
using ZoomIN.App.Settings;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Zoom;

/// <summary>
/// Starts the engine: shortcuts, mouse wheel and activity hooks, zoom output, focus tracking, the
/// filter per window and the docked strip reservation. The commands themselves are in
/// <see cref="ZoomCommands"/>, <see cref="WindowFilterCommands"/> and <see cref="HotkeyBindings"/>.
/// Saving is done by <see cref="SettingsService"/>.
/// </summary>
public sealed class ZoomController(
    SettingsService settings, HotkeyBindings hotkeys, ZoomCommands zoom, IZoomOutput output, InputSources inputs,
    DockedStripSync dockedStrip) : IDisposable
{
    public ZoomStatus Status => output.Status;

    public void Initialize()
    {
        var models = settings.Models;
        hotkeys.Register();
        inputs.Wheel.Start(settings.Current.WheelModifiers, zoom.ZoomBy);
        inputs.Activity.Start(new PointerActivity(models.Effects));
        models.Zoom.Changed += dockedStrip.Update;
        output.Start();
        inputs.Tracking.Start(models.Tracking);
        inputs.WindowFilter.Start(models.Windows);
    }

    public void Dispose() => settings.Models.Zoom.Changed -= dockedStrip.Update;
}
