using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.Settings;

/// <summary>
/// Persisted user settings (ADR-0004). M2/M3 keep only what zoom and input need; M7 adds the
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

    /// <summary>Last level in shared mode, and the start level for monitors without their own.</summary>
    public double LastZoomLevel { get; set; } = ZoomLimits.DefaultLevel;

    /// <summary>Each monitor keeps its own zoom level (M3 decision: default on).</summary>
    public bool PerMonitorZoom { get; set; } = true;

    /// <summary>Last level per monitor device name, e.g. "\\.\DISPLAY1".</summary>
    public IReadOnlyDictionary<string, double> MonitorZoomLevels { get; set; } = new Dictionary<string, double>();

    /// <summary>Glide duration for zoom changes in milliseconds; 0 = instant.</summary>
    public int AnimationMilliseconds { get; set; } = 150;

    public HotkeyGesture ToggleHotkey { get; set; } = HotkeyGesture.ToggleZoom;

    public HotkeyGesture ZoomInHotkey { get; set; } = HotkeyGesture.ZoomIn;

    public HotkeyGesture ZoomOutHotkey { get; set; } = HotkeyGesture.ZoomOut;

    public HotkeyModifiers WheelModifiers { get; set; } = HotkeyModifiers.Control;
}
