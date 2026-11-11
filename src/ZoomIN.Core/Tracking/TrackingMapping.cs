using ZoomIN.Core.Settings;

namespace ZoomIN.Core.Tracking;

/// <summary>Maps between persisted <see cref="AppSettings"/> and the runtime <see cref="TrackingModel"/> (M14 step 9).</summary>
public static class TrackingMapping
{
    public static TrackingModel Create(AppSettings settings) => new(Options(settings));

    public static TrackingOptions Options(AppSettings s) => s is null ? throw new ArgumentNullException(nameof(s)) : new(
        s.TrackingEnabled,
        s.TrackFocus,
        s.TrackCaret,
        Math.Max(0, s.PanMilliseconds) / 1000.0,
        Math.Clamp(s.CaretMargin, 0, 0.4),
        s.FocusFrame,
        s.CaretMarker,
        Enum.IsDefined(s.MouseFollow) ? s.MouseFollow : TrackingOptions.Default.MouseFollow,
        double.IsFinite(s.MouseMargin) ? Math.Clamp(s.MouseMargin, 0, TrackingOptions.MaxMouseMargin) : TrackingOptions.Default.MouseMargin);

    public static AppSettings Apply(AppSettings settings, TrackingModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var o = model.Options;
        return settings with
        {
            TrackingEnabled = o.Enabled,
            TrackFocus = o.Focus,
            TrackCaret = o.Caret,
            PanMilliseconds = (int)Math.Round(o.PanSeconds * 1000),
            CaretMargin = o.CaretMargin,
            FocusFrame = o.FocusFrame,
            CaretMarker = o.CaretMarker,
            MouseFollow = o.MouseFollow,
            MouseMargin = o.MouseMargin,
        };
    }
}
