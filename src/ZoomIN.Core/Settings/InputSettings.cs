using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Tracking;

namespace ZoomIN.Core.Settings;

/// <summary>
/// The input part of <see cref="AppSettings"/>: shortcuts, wheel modifiers and the hold key, all
/// shared by all profiles. A base record only to keep AppSettings within the file-size rule: the
/// properties are inherited, so the settings file keeps them at its top level as before (M18).
/// </summary>
/// <remarks>Properties use <c>set</c>, not <c>init</c>, for the same reason as in <see cref="AppSettings"/>.</remarks>
public abstract record InputSettings
{
    public HotkeyGesture ToggleHotkey { get; set; } = HotkeyGesture.ToggleZoom;

    public HotkeyGesture ZoomInHotkey { get; set; } = HotkeyGesture.ZoomIn;

    public HotkeyGesture ZoomOutHotkey { get; set; } = HotkeyGesture.ZoomOut;

    public HotkeyGesture FullscreenHotkey { get; set; } = HotkeyGesture.Fullscreen;

    public HotkeyGesture LensHotkey { get; set; } = HotkeyGesture.Lens;

    public HotkeyGesture DockedHotkey { get; set; } = HotkeyGesture.Docked;

    public HotkeyGesture FilterHotkey { get; set; } = HotkeyGesture.ToggleFilter;

    public HotkeyGesture NextFilterHotkey { get; set; } = HotkeyGesture.NextFilter;

    public HotkeyGesture LocateHotkey { get; set; } = HotkeyGesture.Locate;

    public HotkeyGesture CrosshairHotkey { get; set; } = HotkeyGesture.ToggleCrosshair;

    public HotkeyGesture TrackingHotkey { get; set; } = HotkeyGesture.ToggleTracking;

    public HotkeyGesture ProfileHotkey { get; set; } = HotkeyGesture.NextProfile;

    /// <summary>Filter for the active window on/off (ADR-0020); unassigned from the start.</summary>
    public HotkeyGesture WindowFilterHotkey { get; set; }

    /// <summary>Overview at the push of a key (B2, M17).</summary>
    public HotkeyGesture OverviewHotkey { get; set; } = HotkeyGesture.Overview;

    /// <summary>Islands on/off and their edit mode (M19 B1).</summary>
    public HotkeyGesture IslandsHotkey { get; set; } = HotkeyGesture.ToggleIslands;

    public HotkeyGesture IslandEditHotkey { get; set; } = HotkeyGesture.EditIslands;
    /// <summary>Freeze the monitor under the pointer / unfreeze all, and pointer to the next monitor (G2, M19).</summary>
    public HotkeyGesture FreezeMonitorHotkey { get; set; } = HotkeyGesture.FreezeMonitor;

    public HotkeyGesture NextMonitorHotkey { get; set; } = HotkeyGesture.NextMonitor;

    public HotkeyModifiers WheelModifiers { get; set; } = HotkeyModifiers.Control;

    /// <summary>Key that shows highlights set to "only while held" (E1).</summary>
    public MarkerHoldKey MarkerHoldKey { get; set; }

    /// <summary>Mouse buffer and axis lock (A6, M19); global, not in the profile; both off from the factory.</summary>
    public PointerConstraintOptions PointerConstraint { get; set; } = PointerConstraintOptions.Default;
}
