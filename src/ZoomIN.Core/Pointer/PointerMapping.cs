using ZoomIN.Core.Settings;

namespace ZoomIN.Core.Pointer;

/// <summary>
/// Maps the pointer fields of the persisted <see cref="AppSettings"/> to <see cref="PointerStyle"/> and
/// back (M14 step 9). Colours are stored as hex; an unreadable one falls back to the default.
/// </summary>
public static class PointerMapping
{
    public static PointerStyle Style(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var d = PointerStyle.Default;
        return new PointerStyle
        {
            MaxScale = settings.CursorMaxScale,
            UnzoomedScale = settings.CursorUnzoomedScale,
            OverrideWindows = settings.CursorOverrideWindows,
            GrowWithZoom = settings.CursorGrowWithZoom,
            ColorMode = settings.CursorColorMode,
            Color = RgbColor.ParseOrDefault(settings.CursorColor, d.Color),
            Rim = settings.CursorRim,
            RimColor = RgbColor.ParseOrDefault(settings.CursorRimColor, d.RimColor),
            RimWidth = settings.CursorRimWidth,
            LocatorColor = RgbColor.ParseOrDefault(settings.LocatorColor, d.LocatorColor),
            LocatorSize = settings.LocatorSize,
            LocatorSeconds = settings.LocatorSeconds,
            LocatorTriggers = settings.LocatorTriggers,
            CrosshairEnabled = settings.CrosshairEnabled,
            CrosshairAlways = settings.CrosshairAlways,
            CrosshairColor = RgbColor.ParseOrDefault(settings.CrosshairColor, d.CrosshairColor),
            CrosshairWidth = settings.CrosshairWidth,
            CrosshairOpacity = settings.CrosshairOpacity,
            HideWhileTyping = settings.HideCursorWhileTyping,
            CrosshairShow = Enum.IsDefined(settings.CrosshairShow) ? settings.CrosshairShow : d.CrosshairShow,
        };
    }

    public static AppSettings Apply(AppSettings settings, PointerStyle p)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(p);
        return settings with
        {
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
            CrosshairShow = p.CrosshairShow,
        };
    }
}
