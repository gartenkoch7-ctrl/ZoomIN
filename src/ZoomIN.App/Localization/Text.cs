using ZoomIN.Core.Localization;
using ZoomIN.Core.Settings;

namespace ZoomIN.App.Localization;

/// <summary>
/// Static facade over the app's <see cref="ILocalizer"/> for <c>x:Bind</c> in XAML, which can
/// only bind to static members without a view model (ADR-0025 decision 3). Code with a
/// constructor takes <see cref="ILocalizer"/> from DI instead.
/// </summary>
public static class Text
{
    // Until the composition root hands over the app's instance (roles without DI, tests): English.
    private static ILocalizer s_localizer = AppTexts.CreateLocalizer();

    public static event Action? LanguageChanged
    {
        add => s_localizer.LanguageChanged += value;
        remove => s_localizer.LanguageChanged -= value;
    }

    public static bool IsGerman => s_localizer.IsGerman;

    /// <summary>Composition root only, before any page or subscriber exists.</summary>
    public static void Use(ILocalizer localizer) => s_localizer = localizer;

    public static void SetLanguage(AppLanguage language) => s_localizer.SetLanguage(language);

    public static string Get(string key) => s_localizer.Get(key);

    public static string Format(string key, params object[] args) => s_localizer.Format(key, args);

    // Modes, states
    public static string ModeFullscreen => Get(nameof(ModeFullscreen));
    public static string ModeLens => Get(nameof(ModeLens));
    public static string ModeDocked => Get(nameof(ModeDocked));

    // Fully qualified: the text ZoomMode below hides the enum inside this class.
    public static string ModeName(Core.Modes.ZoomMode mode) => mode switch
    {
        Core.Modes.ZoomMode.Lens => ModeLens,
        Core.Modes.ZoomMode.Docked => ModeDocked,
        _ => ModeFullscreen,
    };

    public static string On => Get(nameof(On));
    public static string Off => Get(nameof(Off));

    // Tray
    public static string TraySettings => Get(nameof(TraySettings));
    public static string TrayToggle => Get(nameof(TrayToggle));
    public static string TrayNextProfile => Get(nameof(TrayNextProfile));
    public static string TrayExit => Get(nameof(TrayExit));
    public static string CloseToTrayTitle => Get(nameof(CloseToTrayTitle));
    public static string CloseToTrayText => Get(nameof(CloseToTrayText));
    public static string UpdateTitle => Get(nameof(UpdateTitle));

    // Navigation
    public static string NavGeneral => Get(nameof(NavGeneral));
    public static string NavZoom => Get(nameof(NavZoom));
    public static string NavTracking => Get(nameof(NavTracking));
    public static string NavHotkeys => Get(nameof(NavHotkeys));
    public static string NavProfiles => Get(nameof(NavProfiles));
    public static string NavInfo => Get(nameof(NavInfo));

    // General
    public static string GeneralLanguage => Get(nameof(GeneralLanguage));
    public static string LanguageSystem => Get(nameof(LanguageSystem));
    public static string GeneralAutostart => Get(nameof(GeneralAutostart));
    public static string GeneralUpdates => Get(nameof(GeneralUpdates));
    public static string GeneralUpdatesHint => Get(nameof(GeneralUpdatesHint));

    // Zoom
    public static string ZoomMode => Get(nameof(ZoomMode));
    public static string ZoomPerMonitor => Get(nameof(ZoomPerMonitor));
    public static string ZoomAnimation => Get(nameof(ZoomAnimation));
    public static string ZoomLensWidth => Get(nameof(ZoomLensWidth));
    public static string ZoomLensHeight => Get(nameof(ZoomLensHeight));
    public static string ZoomDockEdge => Get(nameof(ZoomDockEdge));
    public static string DockTop => Get(nameof(DockTop));
    public static string DockBottom => Get(nameof(DockBottom));
    public static string DockLeft => Get(nameof(DockLeft));
    public static string DockRight => Get(nameof(DockRight));
    public static string ZoomDockSize => Get(nameof(ZoomDockSize));
    public static string ZoomSmoothing => Get(nameof(ZoomSmoothing));
    public static string SmoothingText => Get(nameof(SmoothingText));
    public static string SmoothingSoft => Get(nameof(SmoothingSoft));
    public static string SmoothingPixel => Get(nameof(SmoothingPixel));

    // Colours (first-run assistant)
    public static string ColorsFilterOn => Get(nameof(ColorsFilterOn));
    public static string ColorsFilter => Get(nameof(ColorsFilter));
    public static string ColorsContrast => Get(nameof(ColorsContrast));
    public static string ColorsBrightness => Get(nameof(ColorsBrightness));

    // Tracking
    public static string TrackingOn => Get(nameof(TrackingOn));
    public static string TrackingFocus => Get(nameof(TrackingFocus));
    public static string TrackingCaret => Get(nameof(TrackingCaret));
    public static string TrackingPan => Get(nameof(TrackingPan));
    public static string TrackingMargin => Get(nameof(TrackingMargin));
    public static string TrackingFocusFrame => Get(nameof(TrackingFocusFrame));
    public static string TrackingCaretMarker => Get(nameof(TrackingCaretMarker));
    public static string TrackingCenterMouse => Get(nameof(TrackingCenterMouse));

    // Hotkeys
    public static string HotkeysIntro => Get(nameof(HotkeysIntro));
    public static string HotkeyChange => Get(nameof(HotkeyChange));
    public static string HotkeyPress => Get(nameof(HotkeyPress));
    public static string HotkeyAltGr => Get(nameof(HotkeyAltGr));
    public static string HotkeyNeedsModifier => Get(nameof(HotkeyNeedsModifier));
    public static string HotkeyUnavailable => Get(nameof(HotkeyUnavailable));

    // Profiles
    public static string ProfilesActive => Get(nameof(ProfilesActive));
    public static string ProfilesName => Get(nameof(ProfilesName));
    public static string ProfilesCreate => Get(nameof(ProfilesCreate));
    public static string ProfilesActivate => Get(nameof(ProfilesActivate));
    public static string ProfilesRename => Get(nameof(ProfilesRename));
    public static string ProfilesDelete => Get(nameof(ProfilesDelete));
    public static string ProfilesExport => Get(nameof(ProfilesExport));
    public static string ProfilesImport => Get(nameof(ProfilesImport));
    public static string ProfilesInvalidName => Get(nameof(ProfilesInvalidName));

    // Info
    public static string InfoZoom => Get(nameof(InfoZoom));
    public static string InfoFps => Get(nameof(InfoFps));
    public static string InfoCapture => Get(nameof(InfoCapture));
    public static string InfoVersion => Get(nameof(InfoVersion));
    public static string InfoLogs => Get(nameof(InfoLogs));
    public static string InfoLicense => Get(nameof(InfoLicense));
    public static string InfoError => Get(nameof(InfoError));

    // First-run assistant
    public static string WelcomeTitle => Get(nameof(WelcomeTitle));
    public static string WelcomeBack => Get(nameof(WelcomeBack));
    public static string WelcomeNext => Get(nameof(WelcomeNext));
    public static string WelcomeFinish => Get(nameof(WelcomeFinish));
}
