using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Filters;

/// <summary>
/// One desktop area that gets its own colour matrix instead of the monitor's filter. The renderer
/// supports exactly one per frame; today a visible toast fills it (identity = original colours),
/// later sources (e.g. a filter for a shared screen area) choose here which one wins.
/// </summary>
/// <param name="Area">Physical desktop coordinates; may reach onto other monitors.</param>
/// <param name="Matrix">Applied to pixels inside <paramref name="Area"/>.</param>
public readonly record struct ColorRegion(PhysicalRect Area, ColorMatrix Matrix)
{
    /// <summary>The region for this frame, or null when the whole view uses the monitor's filter.</summary>
    public static ColorRegion? For(TrackingSnapshot tracking, TrackingOptions options) =>
        options.NotificationColors && tracking.Notification is { } toast
            ? new ColorRegion(toast, ColorMatrix.Identity)
            : null;
}
