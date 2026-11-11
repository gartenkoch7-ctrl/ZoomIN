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
        s.CenterMouse)
    {
        CaretStyle = CaretMarkerStyle.From(s.CaretMarkerShape, s.CaretMarkerColor, s.CaretMarkerThickness),
        FocusFrameShow = Enum.IsDefined(s.FocusFrameShow) ? s.FocusFrameShow : ShowCondition.Always,
        CaretShow = Enum.IsDefined(s.CaretMarkerShow) ? s.CaretMarkerShow : ShowCondition.Always,
    };

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
            CenterMouse = o.CenterMouse,
            CaretMarkerShape = o.CaretStyle.Shape,
            CaretMarkerColor = o.CaretStyle.Color.ToHex(),
            CaretMarkerThickness = o.CaretStyle.Thickness,
            FocusFrameShow = o.FocusFrameShow,
            CaretMarkerShow = o.CaretShow,
        };
    }
}
