using ZoomIN.Core.Filters;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Zoom;

/// <summary>
/// Zoom, mode and filter commands from hotkeys, mouse wheel, tray and settings pages. They apply to
/// the monitor under the mouse (M3), or to all monitors if they share one level or filter.
/// </summary>
public sealed class ZoomCommands(ZoomModel model, EffectsModel effects, IMonitorProvider monitors, ICursorSource cursor)
{
    public IReadOnlyList<string> MonitorIds() => monitors.MonitorIds();

    public string MonitorUnderMouse() =>
        monitors.MonitorAt(cursor.TryGetPosition(out var position) ? position : default);

    public void Toggle() => model.Toggle(MonitorUnderMouse(), monitors.MonitorIds());

    public void Activate(ZoomMode mode) => model.Activate(mode, MonitorUnderMouse(), monitors.MonitorIds());

    /// <summary>Mode chosen in the settings: switches over if zooming, otherwise only preselects it.</summary>
    public void SelectMode(ZoomMode mode)
    {
        if (mode == model.Mode)
        {
            return;
        }

        if (model.IsOn)
        {
            Activate(mode);
        }
        else
        {
            model.SetMode(mode);
        }
    }

    /// <summary>Zoom in (+) or out (−) by mouse-wheel notches on the monitor under the mouse.</summary>
    public void ZoomBy(double notches) => model.Step(notches, MonitorUnderMouse(), monitors.MonitorIds());

    /// <summary>Colour filter on/off for the monitor under the mouse (or all, if monitors share it).</summary>
    public void ToggleFilter() => effects.ToggleFilter(MonitorUnderMouse());

    public void NextFilter() => effects.NextFilter(MonitorUnderMouse());
}
