using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;
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
/// breaking changes; new optional properties with defaults need no migration. Shortcuts and the
/// other settings shared by all profiles are in the base records <see cref="SharedSettings"/> and <see cref="InputSettings"/>.
/// </summary>
/// <remarks>
/// Properties use <c>set</c>, not <c>init</c>: the System.Text.Json source generator assigns
/// default(T) to missing init-only properties, which would wipe the defaults below when an older
/// file lacks a newer property (found in M3). Treat instances as immutable and use <c>with</c>.
/// </remarks>
public sealed record AppSettings : SharedSettings
{
    /// <summary>2 (M17): mouse follow mode instead of CenterMouse, see <see cref="SettingsMigration"/>.</summary>
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

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

    public FilterId ColorFilter { get; set; } = FilterSettings.Default.Filter;

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

    /// <summary>"Schärfe" 0–1 and "Fett" 0–3 px (M18, D1, see TextEnhancement); off from the factory.</summary>
    public double TextSharpness { get; set; }

    public double TextBold { get; set; }

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

    /// <summary>Caret marker look (E2); the factory setting looks like the bar before E2.</summary>
    public CaretMarkerShape CaretMarkerShape { get; set; } = CaretMarkerStyle.Default.Shape;

    public string CaretMarkerColor { get; set; } = CaretMarkerStyle.Default.Color.ToHex();

    public CaretMarkerThickness CaretMarkerThickness { get; set; } = CaretMarkerStyle.Default.Thickness;

    /// <summary>When crosshair, caret marker and focus frame are shown (E1); factory: always.</summary>
    public ShowCondition CrosshairShow { get; set; }

    public ShowCondition CaretMarkerShow { get; set; }

    public ShowCondition FocusFrameShow { get; set; }

    /// <summary>Glide duration for zoom changes in milliseconds; 0 = instant.</summary>
    public int AnimationMilliseconds { get; set; } = 150;

    /// <summary>Zoom while moving or resizing windows (G1): lift to 1× by default, for both.</summary>
    public WindowDragOptions WindowDrag { get; set; } = WindowDragOptions.Default;

    /// <summary>Islands of the profile (M19 B1): shown or hidden, and up to four of them.</summary>
    public IslandOptions Islands { get; set; } = IslandOptions.Default;
}
