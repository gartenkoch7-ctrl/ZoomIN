using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Updates;
using ZoomIN.Core.WindowDrag;
using ZoomIN.Core.WindowFilters;
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
    /// <summary>2 (M17): mouse follow mode instead of CenterMouse, see <see cref="SettingsMigration"/>.</summary>
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    // --- Application (shared by all profiles) ---
    public AppLanguage Language { get; set; } = AppLanguage.System;

    /// <summary>False until the first-run assistant was completed (M7).</summary>
    public bool FirstRunCompleted { get; set; }

    /// <summary>The "closes to tray" hint is shown only once.</summary>
    public bool CloseToTrayHintShown { get; set; }

    /// <summary>The migration to the margin mode (M17 A1) still has to tell the user once where to change it back.</summary>
    public bool MouseFollowHintPending { get; set; }

    /// <summary>Update check at start and once a day (ADR-0014); only asks GitHub for the release list.</summary>
    public bool CheckForUpdates { get; set; }

    /// <summary>Which releases are offered (ADR-0014): finished ones only, or also experimental builds.</summary>
    public ReleaseChannel UpdateChannel { get; set; } = ReleaseChannel.Stable;

    public string ActiveProfile { get; set; } = ProfileFields.DefaultProfileName;

    // --- Diagnostics (shared by all profiles; ADR-0019, read through AppSettingsTelemetry) ---
    public TelemetryLevel TelemetryLevel { get; set; } = TelemetryOptions.Default.Level;

    public bool TelemetryLogs { get; set; } = TelemetryOptions.Default.Logs;

    public bool TelemetryTraces { get; set; } = TelemetryOptions.Default.Traces;

    public bool TelemetryMetrics { get; set; } = TelemetryOptions.Default.Metrics;

    public LogLevel TelemetryLogLevel { get; set; } = TelemetryOptions.Default.LogLevel;

    public int TelemetryRetentionDays { get; set; } = TelemetryOptions.Default.RetentionDays;

    public DateTimeOffset? TelemetryRaisedUntil { get; set; }

    public string TelemetryOtlpEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// KI-Debug (ADR-0027): an agent may read the state, take debug snapshots and trigger shortcuts
    /// over a pipe. Off from the factory; stays on until the user turns it off (user's decision).
    /// </summary>
    public bool AgentDebugEnabled { get; set; }

    /// <summary>Filter per window (ADR-0020): on/off, automatic filter, sensitivity and the user's rules per app + window class.</summary>
    public WindowFilterOptions WindowFilter { get; set; } = WindowFilterOptions.Default;

    // --- Display (stored per profile, see ProfileFields) ---

    /// <summary>
    /// Zoom was on when last saved. Only the secure-desktop instance starts with it, so the lock
    /// screen looks like the desktop just left (ADR-0016); a normal start begins unzoomed.
    /// </summary>
    public bool ZoomOn { get; set; }

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

    /// <summary>Each monitor may keep its own filter on/off and kind (decision: default on, like the zoom level).</summary>
    public bool PerMonitorFilter { get; set; } = true;

    /// <summary>Own filter per monitor device name; monitors without an entry use FilterEnabled/ColorFilter.</summary>
    public IReadOnlyDictionary<string, MonitorFilter> MonitorFilters { get; set; } = new Dictionary<string, MonitorFilter>();

    /// <summary>1 = unchanged; 0.5–3.</summary>
    public double Contrast { get; set; } = 1.0;

    /// <summary>0 = unchanged; −0.5–0.5.</summary>
    public double Brightness { get; set; }

    public Smoothing Smoothing { get; set; } = Smoothing.Text;

    /// <summary>Smart inversion leaves photos and videos original (ADR-0024); on from the start.</summary>
    public bool ProtectPhotos { get; set; } = true;

    // --- Pointer (pointer page); colours are "#RRGGBB" ---

    /// <summary>The magnified pointer grows with the zoom up to this factor.</summary>
    public double CursorMaxScale { get; set; } = PointerStyle.Default.MaxScale;

    /// <summary>Pointer size without zoom; above 1 ZoomIN draws the pointer itself.</summary>
    public double CursorUnzoomedScale { get; set; } = PointerStyle.Default.UnzoomedScale;

    /// <summary>ZoomIN's pointer size and colour replace the Windows pointer settings.</summary>
    public bool CursorOverrideWindows { get; set; } = PointerStyle.Default.OverrideWindows;

    /// <summary>False: the pointer keeps its size at every zoom level.</summary>
    public bool CursorGrowWithZoom { get; set; } = PointerStyle.Default.GrowWithZoom;

    public PointerColorMode CursorColorMode { get; set; } = PointerStyle.Default.ColorMode;

    public string CursorColor { get; set; } = PointerStyle.Default.Color.ToHex();

    public bool CursorRim { get; set; } = PointerStyle.Default.Rim;

    public string CursorRimColor { get; set; } = PointerStyle.Default.RimColor.ToHex();

    public double CursorRimWidth { get; set; } = PointerStyle.Default.RimWidth;

    public string LocatorColor { get; set; } = PointerStyle.Default.LocatorColor.ToHex();

    public double LocatorSize { get; set; } = PointerStyle.Default.LocatorSize;

    public double LocatorSeconds { get; set; } = PointerStyle.Default.LocatorSeconds;

    /// <summary>What shows the locator (decision: hotkey, shaking, after a rest; Ctrl alone off).</summary>
    public LocatorTriggers LocatorTriggers { get; set; } = PointerStyle.Default.LocatorTriggers;

    public bool CrosshairEnabled { get; set; }

    /// <summary>False: crosshair only while zoomed.</summary>
    public bool CrosshairAlways { get; set; }

    public string CrosshairColor { get; set; } = PointerStyle.Default.CrosshairColor.ToHex();

    public double CrosshairWidth { get; set; } = PointerStyle.Default.CrosshairWidth;

    public double CrosshairOpacity { get; set; } = PointerStyle.Default.CrosshairOpacity;

    public bool HideCursorWhileTyping { get; set; }

    /// <summary>Focus and caret tracking on (Strg+Alt+T); the mouse is always tracked.</summary>
    public bool TrackingEnabled { get; set; } = true;

    public bool TrackFocus { get; set; } = true;

    public bool TrackCaret { get; set; } = true;

    /// <summary>Glide time to focus/caret/mouse in milliseconds; 0 = jump (M6 decision: glide by default).</summary>
    public int PanMilliseconds { get; set; } = 200;

    /// <summary>The view follows the caret once it is this close to the edge (0.2 = 20 %).</summary>
    public double CaretMargin { get; set; } = 0.2;

    public bool FocusFrame { get; set; } = true;

    public bool CaretMarker { get; set; } = true;

    /// <summary>How the view follows the mouse (M17 A1): middle, margin (factory) or fixpoint.</summary>
    public MouseFollowMode MouseFollow { get; set; } = TrackingOptions.Default.MouseFollow;

    /// <summary>With MouseFollow = Margin: the view moves once the pointer is this close to its edge (0–0.5).</summary>
    public double MouseMargin { get; set; } = TrackingOptions.Default.MouseMargin;

    /// <summary>Before 1.1 (schema 1): "keep the pointer centred". Read once by <see cref="SettingsMigration"/>, never written.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? CenterMouse { get; set; }

    /// <summary>Glide duration for zoom changes in milliseconds; 0 = instant.</summary>
    public int AnimationMilliseconds { get; set; } = 150;

    /// <summary>Zoom while moving or resizing windows (G1): lift to 1× by default, for both.</summary>
    public WindowDragOptions WindowDrag { get; set; } = WindowDragOptions.Default;

    // --- Input (shared by all profiles) ---
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

    public HotkeyModifiers WheelModifiers { get; set; } = HotkeyModifiers.Control;
}
