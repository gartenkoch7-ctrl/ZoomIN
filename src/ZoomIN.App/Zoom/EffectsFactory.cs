using ZoomIN.Core.Filters;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Settings;

namespace ZoomIN.App.Zoom;

/// <summary>Maps between persisted <see cref="AppSettings"/> and the runtime <see cref="EffectsModel"/>.</summary>
internal static class EffectsFactory
{
    public static EffectsModel Create(AppSettings settings) => new(Effects(settings));

    public static DisplayEffects Effects(AppSettings settings) => new(
        new FilterSettings(settings.FilterEnabled, settings.ColorFilter, settings.Contrast, settings.Brightness),
        settings.Smoothing,
        Pointer(settings))
    {
        PerMonitorFilter = settings.PerMonitorFilter,
        MonitorFilters = settings.MonitorFilters,
        ProtectPhotos = settings.ProtectPhotos,
    };

    public static AppSettings Apply(AppSettings settings, EffectsModel model)
    {
        var effects = model.Current;
        var p = effects.Cursor;
        return settings with
        {
            FilterEnabled = effects.Filter.Enabled,
            ColorFilter = effects.Filter.Filter,
            PerMonitorFilter = effects.PerMonitorFilter,
            MonitorFilters = new Dictionary<string, MonitorFilter>(effects.MonitorFilters),
            Contrast = effects.Filter.Contrast,
            Brightness = effects.Filter.Brightness,
            Smoothing = effects.Smoothing,
            ProtectPhotos = effects.ProtectPhotos,
            CursorMaxScale = p.MaxScale,
            CursorUnzoomedScale = p.UnzoomedScale,
            CursorOverrideWindows = p.OverrideWindows,
            CursorGrowWithZoom = p.GrowWithZoom,
            CursorColorMode = p.ColorMode,
            CursorColor = p.Color.ToHex(),
            CursorRim = p.Rim,
            CursorRimColor = p.RimColor.ToHex(),
            CursorRimWidth = p.RimWidth,
            LocatorColor = p.LocatorColor.ToHex(),
            LocatorSize = p.LocatorSize,
            LocatorSeconds = p.LocatorSeconds,
            LocatorTriggers = p.LocatorTriggers,
            CrosshairEnabled = p.CrosshairEnabled,
            CrosshairAlways = p.CrosshairAlways,
            CrosshairColor = p.CrosshairColor.ToHex(),
            CrosshairWidth = p.CrosshairWidth,
            CrosshairOpacity = p.CrosshairOpacity,
            HideCursorWhileTyping = p.HideWhileTyping,
        };
    }

    private static PointerStyle Pointer(AppSettings s)
    {
        var d = PointerStyle.Default;
        return new PointerStyle
        {
            MaxScale = s.CursorMaxScale,
            UnzoomedScale = s.CursorUnzoomedScale,
            OverrideWindows = s.CursorOverrideWindows,
            GrowWithZoom = s.CursorGrowWithZoom,
            ColorMode = s.CursorColorMode,
            Color = RgbColor.ParseOrDefault(s.CursorColor, d.Color),
            Rim = s.CursorRim,
            RimColor = RgbColor.ParseOrDefault(s.CursorRimColor, d.RimColor),
            RimWidth = s.CursorRimWidth,
            LocatorColor = RgbColor.ParseOrDefault(s.LocatorColor, d.LocatorColor),
            LocatorSize = s.LocatorSize,
            LocatorSeconds = s.LocatorSeconds,
            LocatorTriggers = s.LocatorTriggers,
            CrosshairEnabled = s.CrosshairEnabled,
            CrosshairAlways = s.CrosshairAlways,
            CrosshairColor = RgbColor.ParseOrDefault(s.CrosshairColor, d.CrosshairColor),
            CrosshairWidth = s.CrosshairWidth,
            CrosshairOpacity = s.CrosshairOpacity,
            HideWhileTyping = s.HideCursorWhileTyping,
        };
    }
}
