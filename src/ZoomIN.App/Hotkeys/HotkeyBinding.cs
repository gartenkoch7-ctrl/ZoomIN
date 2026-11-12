using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Settings;

namespace ZoomIN.App.Hotkeys;

/// <summary>One editable shortcut: its label key and where it lives in <see cref="AppSettings"/>.</summary>
/// <param name="Optional">May stay unassigned (Del removes it), like the window filter shortcut.</param>
/// <param name="HintKey">Text key of an explanation shown under the name, or null.</param>
public sealed record HotkeyBinding(
    string TextKey, Func<AppSettings, HotkeyGesture> Get, Func<AppSettings, HotkeyGesture, AppSettings> Set, bool Optional = false,
    string? HintKey = null)
{
    public static IReadOnlyList<HotkeyBinding> All { get; } =
    [
        new("Act.Toggle", s => s.ToggleHotkey, (s, g) => s with { ToggleHotkey = g }),
        new("Act.ZoomIn", s => s.ZoomInHotkey, (s, g) => s with { ZoomInHotkey = g }),
        new("Act.ZoomOut", s => s.ZoomOutHotkey, (s, g) => s with { ZoomOutHotkey = g }),
        new("Act.Fullscreen", s => s.FullscreenHotkey, (s, g) => s with { FullscreenHotkey = g }),
        new("Act.Lens", s => s.LensHotkey, (s, g) => s with { LensHotkey = g }),
        new("Act.Docked", s => s.DockedHotkey, (s, g) => s with { DockedHotkey = g }),
        new("Act.Filter", s => s.FilterHotkey, (s, g) => s with { FilterHotkey = g }),
        new("Act.NextFilter", s => s.NextFilterHotkey, (s, g) => s with { NextFilterHotkey = g }),
        new("Act.Locate", s => s.LocateHotkey, (s, g) => s with { LocateHotkey = g }),
        new("Act.Crosshair", s => s.CrosshairHotkey, (s, g) => s with { CrosshairHotkey = g }),
        new("Act.Tracking", s => s.TrackingHotkey, (s, g) => s with { TrackingHotkey = g }),
        new("Act.Profile", s => s.ProfileHotkey, (s, g) => s with { ProfileHotkey = g }),
        new("Act.WindowFilter", s => s.WindowFilterHotkey, (s, g) => s with { WindowFilterHotkey = g }, Optional: true),
        new("Act.Overview", s => s.OverviewHotkey, (s, g) => s with { OverviewHotkey = g }, Optional: true, HintKey: "Act.Overview.Hint"),
        new("Act.FreezeMonitor", s => s.FreezeMonitorHotkey, (s, g) => s with { FreezeMonitorHotkey = g }, Optional: true,
            HintKey: "Act.FreezeMonitor.Hint"),
        new("Act.NextMonitor", s => s.NextMonitorHotkey, (s, g) => s with { NextMonitorHotkey = g }, Optional: true),
    ];
}
