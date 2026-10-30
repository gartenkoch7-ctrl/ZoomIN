using ZoomIN.Core.Filters;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.Settings;

/// <summary>
/// Persisted user settings (ADR-0004). M2–M4 keep only what zoom and input need; M7 adds the
/// rest and the settings UI. Bump <see cref="CurrentSchemaVersion"/> and add a migration on
/// breaking changes; new optional properties with defaults need no migration.
/// </summary>
/// <remarks>
/// Properties use <c>set</c>, not <c>init</c>: the System.Text.Json source generator assigns
/// default(T) to missing init-only properties, which would wipe the defaults below when an older
/// file lacks a newer property (found in M3). Treat instances as immutable and use <c>with</c>.
/// </remarks>
public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Mode used by Strg+Alt+Z.</summary>
    public ZoomMode LastMode { get; set; } = ZoomMode.Fullscreen;

    /// <summary>Each monitor keeps its own zoom level (M3 decision: default on).</summary>
    public bool PerMonitorZoom { get; set; } = true;

    /// <summary>Fullscreen: shared level, and the start level for monitors without their own.</summary>
    public double LastZoomLevel { get; set; } = ZoomLevels.Defaults[ZoomMode.Fullscreen];

    /// <summary>Fullscreen: last level per monitor device name, e.g. "\\.\DISPLAY1".</summary>
    public IReadOnlyDictionary<string, double> MonitorZoomLevels { get; set; } = new Dictionary<string, double>();

    public double LensZoomLevel { get; set; } = ZoomLevels.Defaults[ZoomMode.Lens];

    public IReadOnlyDictionary<string, double> LensMonitorZoomLevels { get; set; } = new Dictionary<string, double>();

    public double DockedZoomLevel { get; set; } = ZoomLevels.Defaults[ZoomMode.Docked];

    public IReadOnlyDictionary<string, double> DockedMonitorZoomLevels { get; set; } = new Dictionary<string, double>();

    /// <summary>Lens size as fractions of the monitor (M4 decision: 40 % × 30 %).</summary>
    public double LensWidth { get; set; } = LensSize.Default.WidthFraction;

    public double LensHeight { get; set; } = LensSize.Default.HeightFraction;

    public DockEdge DockEdge { get; set; } = DockLayout.Default.Edge;

    /// <summary>Docked strip size as a fraction of the monitor (M4 decision: 40 %).</summary>
    public double DockFraction { get; set; } = DockLayout.Default.Fraction;

    public bool FilterEnabled { get; set; }

    public ColorFilter ColorFilter { get; set; } = FilterSettings.Default.Filter;

    /// <summary>1 = unchanged; 0.5–3.</summary>
    public double Contrast { get; set; } = 1.0;

    /// <summary>0 = unchanged; −0.5–0.5.</summary>
    public double Brightness { get; set; }

    public Smoothing Smoothing { get; set; } = Smoothing.Text;

    /// <summary>The magnified pointer grows with the zoom up to this factor.</summary>
    public double CursorMaxScale { get; set; } = CursorStyle.Default.MaxScale;

    public bool CursorRim { get; set; } = true;

    /// <summary>Show the locator when the mouse moves after a pause (M5 decision: off).</summary>
    public bool AutoLocate { get; set; }

    /// <summary>Glide duration for zoom changes in milliseconds; 0 = instant.</summary>
    public int AnimationMilliseconds { get; set; } = 150;

    public HotkeyGesture ToggleHotkey { get; set; } = HotkeyGesture.ToggleZoom;

    public HotkeyGesture ZoomInHotkey { get; set; } = HotkeyGesture.ZoomIn;

    public HotkeyGesture ZoomOutHotkey { get; set; } = HotkeyGesture.ZoomOut;

    public HotkeyGesture FullscreenHotkey { get; set; } = HotkeyGesture.Fullscreen;

    public HotkeyGesture LensHotkey { get; set; } = HotkeyGesture.Lens;

    public HotkeyGesture DockedHotkey { get; set; } = HotkeyGesture.Docked;

    public HotkeyGesture FilterHotkey { get; set; } = HotkeyGesture.ToggleFilter;

    public HotkeyGesture NextFilterHotkey { get; set; } = HotkeyGesture.NextFilter;

    public HotkeyGesture LocateHotkey { get; set; } = HotkeyGesture.Locate;

    public HotkeyModifiers WheelModifiers { get; set; } = HotkeyModifiers.Control;
}
