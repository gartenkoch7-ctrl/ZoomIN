using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Settings;

namespace ZoomIN.App.Hotkeys;

/// <summary>One editable shortcut: its label key and where it lives in <see cref="AppSettings"/>.</summary>
/// <param name="Optional">May stay unassigned (Del removes it), like the window filter shortcut.</param>
public sealed record HotkeyBinding(
    string TextKey, Func<AppSettings, HotkeyGesture> Get, Func<AppSettings, HotkeyGesture, AppSettings> Set, bool Optional = false)
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
    ];
}
