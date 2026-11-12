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
        PerMonitorFilter = profile.PerMonitorFilter,
        MonitorFilters = profile.MonitorFilters,
        Contrast = profile.Contrast,
        Brightness = profile.Brightness,
        Smoothing = profile.Smoothing,
        TextSharpness = profile.TextSharpness,
        TextBold = profile.TextBold,
        CursorMaxScale = profile.CursorMaxScale,
        CursorUnzoomedScale = profile.CursorUnzoomedScale,
        CursorOverrideWindows = profile.CursorOverrideWindows,
        CursorGrowWithZoom = profile.CursorGrowWithZoom,
        CursorColorMode = profile.CursorColorMode,
        CursorColor = profile.CursorColor,
        CursorRim = profile.CursorRim,
        CursorRimColor = profile.CursorRimColor,
        CursorRimWidth = profile.CursorRimWidth,
        LocatorColor = profile.LocatorColor,
        LocatorSize = profile.LocatorSize,
        LocatorSeconds = profile.LocatorSeconds,
        LocatorTriggers = profile.LocatorTriggers,
        CrosshairEnabled = profile.CrosshairEnabled,
        CrosshairAlways = profile.CrosshairAlways,
        CrosshairColor = profile.CrosshairColor,
        CrosshairWidth = profile.CrosshairWidth,
        CrosshairOpacity = profile.CrosshairOpacity,
        HideCursorWhileTyping = profile.HideCursorWhileTyping,
        TrackingEnabled = profile.TrackingEnabled,
        TrackFocus = profile.TrackFocus,
        TrackCaret = profile.TrackCaret,
        PanMilliseconds = profile.PanMilliseconds,
        CaretMargin = profile.CaretMargin,
        FocusFrame = profile.FocusFrame,
        CaretMarker = profile.CaretMarker,
        MouseFollow = profile.MouseFollow,
        MouseMargin = profile.MouseMargin,
        CaretMarkerShape = profile.CaretMarkerShape,
        CaretMarkerColor = profile.CaretMarkerColor,
        CaretMarkerThickness = profile.CaretMarkerThickness,
        CrosshairShow = profile.CrosshairShow,
        CaretMarkerShow = profile.CaretMarkerShow,
        FocusFrameShow = profile.FocusFrameShow,
        AnimationMilliseconds = profile.AnimationMilliseconds,
        WindowDrag = profile.WindowDrag,
    };

    /// <summary>
    /// What a profile file holds: everything but the own colour schemes (M18). Their definitions are
    /// shared and live in settings.json only, so profile files carry no stale copies.
    /// </summary>
    public static AppSettings ForProfileFile(AppSettings settings) => settings with { CustomSchemes = [] };

    /// <summary>Profile names are file names: letters, digits, space, dash and underscore only.</summary>
    public static bool IsValidName(string name) =>
        !string.IsNullOrWhiteSpace(name)
        && name.Length <= 40
        && name.All(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_');
}
