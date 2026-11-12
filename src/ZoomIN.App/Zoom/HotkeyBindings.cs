using Microsoft.Extensions.Logging;
using ZoomIN.App.Freeze;
using ZoomIN.App.Overview;
using ZoomIN.App.Settings;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Pointer;

namespace ZoomIN.App.Zoom;

/// <summary>Which shortcut does what: the gesture → action table, registered with Windows (ADR-0008).</summary>
public sealed class HotkeyBindings(
    SettingsService settings, ProfileService profiles, IHotkeyService hotkeys, ZoomCommands zoom,
    WindowFilterCommands windowFilter, OverviewController overview, MonitorFreezeController freeze,
    MonitorSwitchController monitorSwitch, ILogger<HotkeyBindings> log)
{
    /// <summary>Hotkeys that could not be registered because another application owns them.</summary>
    public IReadOnlyList<HotkeyGesture> Unavailable { get; private set; } = [];

    /// <summary>Registers all shortcuts (again), e.g. after one was changed in the settings.</summary>
    public void Register()
    {
        hotkeys.UnregisterAll();
        var s = settings.Current;
        var models = settings.Models;
        var unavailable = new List<HotkeyGesture>();
        void Add(HotkeyGesture gesture, Action action, bool repeat = false)
        {
            // Unassigned (e.g. the window filter shortcut from the start).
            if (gesture.VirtualKey == 0)
            {
                return;
            }

            if (hotkeys.Register(gesture, action, repeat))
            {
                log.HotkeyRegistered(gesture);
            }
            else
            {
                unavailable.Add(gesture);
            }
        }

        Add(s.ToggleHotkey, zoom.Toggle);
        Add(s.ZoomInHotkey, () => zoom.ZoomBy(1), repeat: true);
        Add(s.ZoomOutHotkey, () => zoom.ZoomBy(-1), repeat: true);
        Add(s.FullscreenHotkey, () => zoom.Activate(ZoomMode.Fullscreen));
        Add(s.LensHotkey, () => zoom.Activate(ZoomMode.Lens));
        Add(s.DockedHotkey, () => zoom.Activate(ZoomMode.Docked));
        Add(HotkeyGesture.LensWider, () => models.Zoom.ResizeLens(1, 0), repeat: true);
        Add(HotkeyGesture.LensNarrower, () => models.Zoom.ResizeLens(-1, 0), repeat: true);
        Add(HotkeyGesture.LensTaller, () => models.Zoom.ResizeLens(0, 1), repeat: true);
        Add(HotkeyGesture.LensShorter, () => models.Zoom.ResizeLens(0, -1), repeat: true);
        Add(s.FilterHotkey, zoom.ToggleFilter);
        Add(s.NextFilterHotkey, zoom.NextFilter);
        if (models.Effects.Current.Cursor.LocatorTriggers.HasFlag(LocatorTriggers.Hotkey))
        {
            Add(s.LocateHotkey, models.Effects.Locate);
        }

        Add(s.CrosshairHotkey, models.Effects.ToggleCrosshair);
        Add(s.TrackingHotkey, models.Tracking.ToggleEnabled);
        Add(s.ProfileHotkey, profiles.Next);
        Add(s.WindowFilterHotkey, () => windowFilter.Toggle());
        Add(s.OverviewHotkey, () => overview.Press());
        var refusedBefore = unavailable.Count;
        Add(s.FreezeMonitorHotkey, freeze.ToggleFreeze);
        var freezeLost = s.FreezeMonitorHotkey.VirtualKey == 0 || unavailable.Count > refusedBefore;
        Add(s.NextMonitorHotkey, monitorSwitch.NextMonitor);
        Unavailable = unavailable;

        // Without Ctrl+Alt+H nothing could give a frozen monitor back (G2).
        if (freezeLost)
        {
            freeze.LiftAll("shortcut_gone");
        }
    }

    /// <summary>Releases all shortcuts while the hotkey editor records a new combination; a freeze ends (G2).</summary>
    public void Suspend()
    {
        hotkeys.UnregisterAll();
        freeze.LiftAll("shortcut_gone");
    }
}
