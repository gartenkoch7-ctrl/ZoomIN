namespace ZoomIN.Core.Settings;

/// <summary>
/// Which settings belong to a profile (M7 decision): everything about the display – mode, levels,
/// lens, dock, filter, pointer, tracking. Hotkeys, language, autostart and updates are shared.
/// </summary>
public static class ProfileFields
{
    public const string DefaultProfileName = "Standard";

    /// <summary>Returns <paramref name="target"/> with all display settings taken from <paramref name="profile"/>.</summary>
    public static AppSettings ApplyDisplay(AppSettings target, AppSettings profile) => target with
    {
        LastMode = profile.LastMode,
        PerMonitorZoom = profile.PerMonitorZoom,
        LastZoomLevel = profile.LastZoomLevel,
        MonitorZoomLevels = profile.MonitorZoomLevels,
        LensZoomLevel = profile.LensZoomLevel,
        LensMonitorZoomLevels = profile.LensMonitorZoomLevels,
        DockedZoomLevel = profile.DockedZoomLevel,
        DockedMonitorZoomLevels = profile.DockedMonitorZoomLevels,
        LensWidth = profile.LensWidth,
        LensHeight = profile.LensHeight,
        DockEdge = profile.DockEdge,
        DockFraction = profile.DockFraction,
        FilterEnabled = profile.FilterEnabled,
        ColorFilter = profile.ColorFilter,
        Contrast = profile.Contrast,
        Brightness = profile.Brightness,
        Smoothing = profile.Smoothing,
        CursorMaxScale = profile.CursorMaxScale,
        CursorRim = profile.CursorRim,
        AutoLocate = profile.AutoLocate,
        TrackingEnabled = profile.TrackingEnabled,
        TrackFocus = profile.TrackFocus,
        TrackCaret = profile.TrackCaret,
        PanMilliseconds = profile.PanMilliseconds,
        CaretMargin = profile.CaretMargin,
        FocusFrame = profile.FocusFrame,
        CaretMarker = profile.CaretMarker,
        AnimationMilliseconds = profile.AnimationMilliseconds,
    };

    /// <summary>Profile names are file names: letters, digits, space, dash and underscore only.</summary>
    public static bool IsValidName(string name) =>
        !string.IsNullOrWhiteSpace(name)
        && name.Length <= 40
        && name.All(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_');
}
