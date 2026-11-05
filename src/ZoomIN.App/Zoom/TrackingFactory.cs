using ZoomIN.Core.Settings;
using ZoomIN.Core.Tracking;

namespace ZoomIN.App.Zoom;

/// <summary>Maps between persisted <see cref="AppSettings"/> and the runtime <see cref="TrackingModel"/>.</summary>
internal static class TrackingFactory
{
    public static TrackingModel Create(AppSettings settings) => new(Options(settings));

    public static TrackingOptions Options(AppSettings s) => new(
        s.TrackingEnabled,
        s.TrackFocus,
        s.TrackCaret,
        Math.Max(0, s.PanMilliseconds) / 1000.0,
        Math.Clamp(s.CaretMargin, 0, 0.4),
        s.FocusFrame,
        s.CaretMarker,
        s.CenterMouse);

    public static AppSettings Apply(AppSettings settings, TrackingModel model)
    {
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
        };
    }
}
