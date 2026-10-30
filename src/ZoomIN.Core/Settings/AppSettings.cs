using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.Settings;

/// <summary>
/// Persisted user settings (ADR-0004). M2 keeps only what zoom and input need; M7 adds the rest
/// and the settings UI. Bump <see cref="CurrentSchemaVersion"/> and add a migration on changes.
/// </summary>
public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public double LastZoomLevel { get; init; } = ZoomLimits.DefaultLevel;

    /// <summary>Glide duration for zoom changes in milliseconds; 0 = instant.</summary>
    public int AnimationMilliseconds { get; init; } = 150;

    public HotkeyGesture ToggleHotkey { get; init; } = HotkeyGesture.ToggleZoom;

    public HotkeyGesture ZoomInHotkey { get; init; } = HotkeyGesture.ZoomIn;

    public HotkeyGesture ZoomOutHotkey { get; init; } = HotkeyGesture.ZoomOut;

    public HotkeyModifiers WheelModifiers { get; init; } = HotkeyModifiers.Control;
}
