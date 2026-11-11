using ZoomIN.Core.Pointer;
using ZoomIN.Core.Settings;

namespace ZoomIN.Core.Filters;

/// <summary>Maps between persisted <see cref="AppSettings"/> and the runtime <see cref="EffectsModel"/> (M14 step 9).</summary>
public static class EffectsMapping
{
    public static EffectsModel Create(AppSettings settings) => new(Effects(settings));

    public static DisplayEffects Effects(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new DisplayEffects(
            new FilterSettings(settings.FilterEnabled, settings.ColorFilter, settings.Contrast, settings.Brightness),
            settings.Smoothing,
            PointerMapping.Style(settings))
        {
            PerMonitorFilter = settings.PerMonitorFilter,
            MonitorFilters = settings.MonitorFilters,
            ProtectPhotos = settings.ProtectPhotos,
            Schemes = settings.CustomSchemes,
        };
    }

    public static AppSettings Apply(AppSettings settings, EffectsModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var effects = model.Current;
        return PointerMapping.Apply(settings, effects.Cursor) with
        {
            FilterEnabled = effects.Filter.Enabled,
            ColorFilter = effects.Filter.Filter,
            PerMonitorFilter = effects.PerMonitorFilter,
            MonitorFilters = new Dictionary<string, MonitorFilter>(effects.MonitorFilters),
            Contrast = effects.Filter.Contrast,
            Brightness = effects.Filter.Brightness,
            Smoothing = effects.Smoothing,
            ProtectPhotos = effects.ProtectPhotos,
            CustomSchemes = effects.Schemes,
        };
    }
}
