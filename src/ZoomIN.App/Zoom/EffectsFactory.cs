using ZoomIN.Core.Filters;
using ZoomIN.Core.Settings;

namespace ZoomIN.App.Zoom;

/// <summary>Maps between persisted <see cref="AppSettings"/> and the runtime <see cref="EffectsModel"/>.</summary>
internal static class EffectsFactory
{
    public static EffectsModel Create(AppSettings settings) => new(Effects(settings));

    public static DisplayEffects Effects(AppSettings settings) => new(
        new FilterSettings(settings.FilterEnabled, settings.ColorFilter, settings.Contrast, settings.Brightness),
        settings.Smoothing,
        new CursorStyle(settings.CursorMaxScale, settings.CursorRim, settings.AutoLocate));

    public static AppSettings Apply(AppSettings settings, EffectsModel model)
    {
        var effects = model.Current;
        return settings with
        {
            FilterEnabled = effects.Filter.Enabled,
            ColorFilter = effects.Filter.Filter,
            Contrast = effects.Filter.Contrast,
            Brightness = effects.Filter.Brightness,
            Smoothing = effects.Smoothing,
            CursorMaxScale = effects.Cursor.MaxScale,
            CursorRim = effects.Cursor.Rim,
            AutoLocate = effects.Cursor.AutoLocate,
        };
    }
}
