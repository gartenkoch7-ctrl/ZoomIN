using Microsoft.Extensions.Logging;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Overview;

/// <summary>
/// The overview frame (B2) outside the render thread, with the same <see cref="OverviewFrame"/>
/// math the renderer draws with: for KI-Debug and for placing the view when the overview ends.
/// </summary>
internal sealed class OverviewFrameSource(
    IMonitorProvider monitors, ICursorSource cursor, ZoomModel zoom, TrackingModel tracking, ViewCenterRequests views,
    TimeProvider time, ILogger log)
{
    /// <summary>The frame with the mouse now, or null if the monitor or the mouse is unknown.</summary>
    public PhysicalRect? Now(OverviewSession session)
    {
        if (monitors.BoundsOf(session.MonitorId) is not { } bounds || !cursor.TryGetPosition(out var mouse))
        {
            return null;
        }

        return OverviewFrame.Rect(
            session, bounds, mouse, zoom.TargetFor(session.MonitorId), zoom.Lens, zoom.Dock, tracking.Options.MouseFollow);
    }

    /// <summary>
    /// Asks the anchor to centre the view on the frame (zoom in) or on the view before the overview
    /// (Esc). Call before the 1× is released, so the glide already heads there. A failure here
    /// (monitor gone) must not keep the end from being completed: it is only logged.
    /// </summary>
    public void PlaceView(OverviewEnded ended)
    {
        var session = ended.Session;
        try
        {
            PhysicalPoint? center = ended.End switch
            {
                OverviewEnd.ZoomIn when Now(session) is { } frame => OverviewFrame.Center(frame),
                OverviewEnd.Escape when session.TryGetStart(out var view, out _) => OverviewFrame.Center(view),
                _ => null,
            };

            if (center is { } point)
            {
                views.Request(session.MonitorId, point, zoom.TargetFor(session.MonitorId), time.GetTimestamp());
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.OverviewFailed(ex, "view_placement");
        }
    }
}
