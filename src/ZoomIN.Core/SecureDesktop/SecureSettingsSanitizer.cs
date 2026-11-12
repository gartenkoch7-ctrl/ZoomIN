using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Updates;
using ZoomIN.Core.WindowFilters;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.SecureDesktop;

/// <summary>
/// Bounds every value of settings that the secure-desktop instance (running as SYSTEM) read from a
/// user-writable file (ADR-0016). The models clamp most numbers themselves; this also covers NaN,
/// unknown enum values, oversized dictionaries and strings, and shortcuts that would block typing.
/// Ranges follow the settings pages.
/// </summary>
public static class SecureSettingsSanitizer
{
    public const int MaxMonitorEntries = 16;
    public const int MaxTextLength = 64;

    private const LocatorTriggers AllTriggers =
        LocatorTriggers.Hotkey | LocatorTriggers.Shake | LocatorTriggers.AfterIdle | LocatorTriggers.CtrlKey;

    private const HotkeyModifiers AllModifiers =
        HotkeyModifiers.Alt | HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Windows;

    private static readonly AppSettings s_defaults = new();

    public static AppSettings Sanitize(AppSettings s)
    {
        var d = s_defaults;
        return s with
        {
            SchemaVersion = AppSettings.CurrentSchemaVersion,
            Language = Defined(s.Language, d.Language),
            UpdateChannel = Defined(s.UpdateChannel, d.UpdateChannel),
            TelemetryLevel = Defined(s.TelemetryLevel, d.TelemetryLevel),
            TelemetryLogLevel = Defined(s.TelemetryLogLevel, d.TelemetryLogLevel),
            TelemetryRetentionDays = Math.Clamp(s.TelemetryRetentionDays, 1, TelemetryOptions.MaxRetentionDays),

            // SYSTEM never sends telemetry anywhere (ADR-0019), whatever the user's file says.
            TelemetryOtlpEndpoint = string.Empty,

            // KI-Debug never runs as SYSTEM (ADR-0016, ADR-0027); the secure host has no pipe anyway.
            AgentDebugEnabled = false,
            ActiveProfile = Text(s.ActiveProfile, d.ActiveProfile),
            LastMode = Defined(s.LastMode, d.LastMode),
            LastZoomLevel = Level(s.LastZoomLevel, d.LastZoomLevel),
            MonitorZoomLevels = Levels(s.MonitorZoomLevels),
            LensZoomLevel = Level(s.LensZoomLevel, d.LensZoomLevel),
            LensMonitorZoomLevels = Levels(s.LensMonitorZoomLevels),
            DockedZoomLevel = Level(s.DockedZoomLevel, d.DockedZoomLevel),
            DockedMonitorZoomLevels = Levels(s.DockedMonitorZoomLevels),
            LensWidth = Number(s.LensWidth, LensSize.MinFraction, LensSize.MaxFraction, d.LensWidth),
            LensHeight = Number(s.LensHeight, LensSize.MinFraction, LensSize.MaxFraction, d.LensHeight),
            DockEdge = Defined(s.DockEdge, d.DockEdge),
            DockFraction = Number(s.DockFraction, 0.15, 0.7, d.DockFraction),
            ColorFilter = Defined(s.ColorFilter, d.ColorFilter),
            MonitorFilters = Filters(s.MonitorFilters),

            // At most 4 schemes with bounded names, colours and replacements (M18).
            CustomSchemes = CustomSchemes.Normalized(s.CustomSchemes),
            Contrast = Number(s.Contrast, 0.5, 3, d.Contrast),
            Brightness = Number(s.Brightness, -0.5, 0.5, d.Brightness),
            Smoothing = Defined(s.Smoothing, d.Smoothing),
            CursorMaxScale = Number(s.CursorMaxScale, 1, 8, d.CursorMaxScale),
            CursorUnzoomedScale = Number(s.CursorUnzoomedScale, 1, 4, d.CursorUnzoomedScale),
            CursorColorMode = Defined(s.CursorColorMode, d.CursorColorMode),
            CursorColor = Color(s.CursorColor, d.CursorColor),
            CursorRimColor = Color(s.CursorRimColor, d.CursorRimColor),
            CursorRimWidth = Number(s.CursorRimWidth, 1, 8, d.CursorRimWidth),
            LocatorColor = Color(s.LocatorColor, d.LocatorColor),
            LocatorSize = Number(s.LocatorSize, 60, 400, d.LocatorSize),
            LocatorSeconds = Number(s.LocatorSeconds, 0.3, 3, d.LocatorSeconds),
            LocatorTriggers = s.LocatorTriggers & AllTriggers,
            CrosshairColor = Color(s.CrosshairColor, d.CrosshairColor),
            CrosshairWidth = Number(s.CrosshairWidth, 1, 10, d.CrosshairWidth),
            CrosshairOpacity = Number(s.CrosshairOpacity, 0.2, 1, d.CrosshairOpacity),
            PanMilliseconds = Math.Clamp(s.PanMilliseconds, 0, 1000),
            CaretMargin = Number(s.CaretMargin, 0, 0.4, d.CaretMargin),
            MouseFollow = Defined(s.MouseFollow, d.MouseFollow),
            CenterMouse = null, // schema 1 only; the copy is written as schema 2
            MouseMargin = Number(s.MouseMargin, 0, TrackingOptions.MaxMouseMargin, d.MouseMargin),
            AnimationMilliseconds = Math.Clamp(s.AnimationMilliseconds, 0, 500),
            ToggleHotkey = Hotkey(s.ToggleHotkey, d.ToggleHotkey),
            ZoomInHotkey = Hotkey(s.ZoomInHotkey, d.ZoomInHotkey),
            ZoomOutHotkey = Hotkey(s.ZoomOutHotkey, d.ZoomOutHotkey),
            FullscreenHotkey = Hotkey(s.FullscreenHotkey, d.FullscreenHotkey),
            LensHotkey = Hotkey(s.LensHotkey, d.LensHotkey),
            DockedHotkey = Hotkey(s.DockedHotkey, d.DockedHotkey),
            FilterHotkey = Hotkey(s.FilterHotkey, d.FilterHotkey),
            NextFilterHotkey = Hotkey(s.NextFilterHotkey, d.NextFilterHotkey),
            LocateHotkey = Hotkey(s.LocateHotkey, d.LocateHotkey),
            CrosshairHotkey = Hotkey(s.CrosshairHotkey, d.CrosshairHotkey),
            TrackingHotkey = Hotkey(s.TrackingHotkey, d.TrackingHotkey),
            ProfileHotkey = Hotkey(s.ProfileHotkey, d.ProfileHotkey),

            // Filter per window off on the secure desktop (ADR-0020): no window tracking there.
            WindowFilter = new WindowFilterOptions { Enabled = false },
            WindowFilterHotkey = default,

            // Overview not on the secure desktop for now (B2 plan: low-level hook and UIAccess there unchecked).
            OverviewHotkey = default,
            WheelModifiers = Wheel(s.WheelModifiers, d.WheelModifiers),
        };
    }

    private static T Defined<T>(T value, T fallback)
        where T : struct, Enum => Enum.IsDefined(value) ? value : fallback;

    private static double Number(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

    private static double Level(double value, double fallback) =>
        double.IsFinite(value) ? ZoomLimits.Clamp(value) : fallback;

    private static string Text(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) || value.Length > MaxTextLength ? fallback : value;

    // Normalised through RgbColor so only "#RRGGBB" survives.
    private static string Color(string? value, string fallback) => RgbColor.NormalizedHex(value, fallback);

    private static Dictionary<string, double> Levels(IReadOnlyDictionary<string, double>? levels)
    {
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (key, value) in levels ?? new Dictionary<string, double>())
        {
            if (result.Count < MaxMonitorEntries && IsMonitorKey(key) && double.IsFinite(value))
            {
                result[key] = ZoomLimits.Clamp(value);
            }
        }

        return result;
    }

    private static Dictionary<string, MonitorFilter> Filters(IReadOnlyDictionary<string, MonitorFilter>? filters)
    {
        var result = new Dictionary<string, MonitorFilter>(StringComparer.Ordinal);
        foreach (var (key, value) in filters ?? new Dictionary<string, MonitorFilter>())
        {
            if (result.Count < MaxMonitorEntries && IsMonitorKey(key) && value is not null && Enum.IsDefined(value.Filter))
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static bool IsMonitorKey(string? key) => !string.IsNullOrEmpty(key) && key.Length <= MaxTextLength;

    // Same rules as the hotkey editor: a hotkey must not block a character in the password field.
    private static HotkeyGesture Hotkey(HotkeyGesture gesture, HotkeyGesture fallback) =>
        (gesture.Modifiers & ~AllModifiers) == 0
        && HotkeyRules.HasModifier(gesture)
        && !HotkeyRules.BlocksAltGrCharacter(gesture)
        && !HotkeyRules.IsModifierKey(gesture.VirtualKey)
        && gesture.VirtualKey is > 0 and < 0xFF
            ? gesture
            : fallback;

    private static HotkeyModifiers Wheel(HotkeyModifiers modifiers, HotkeyModifiers fallback) =>
        (modifiers & ~AllModifiers) == 0 && (modifiers & (HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Windows)) != 0
            ? modifiers
            : fallback;
}
