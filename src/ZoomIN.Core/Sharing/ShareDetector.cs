using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Sharing;

/// <summary>
/// Recognises an active share from the meeting app's windows (ADR-0018). While sharing, the app
/// draws a frame around what is shared: a topmost, click-through window as large as the monitor
/// or the shared window. Window classes and titles are not used, they change with app updates.
/// </summary>
public static class ShareDetector
{
    /// <summary>Smaller click-through windows are toasts or tooltips, not a share frame.</summary>
    public const double MinFrameSize = 150;

    /// <summary>A frame whose edges lie this close to a monitor's edges frames the whole monitor.</summary>
    public const double ScreenTolerance = 8;

    public static SharedArea? Detect(IReadOnlyList<MeetingWindow> windows, IReadOnlyList<PhysicalRect> monitors) =>
        FindFrame(windows) is { } frame ? Classify(frame.Bounds, monitors) : null;

    /// <summary>The share frame among the windows: the largest topmost, click-through one.</summary>
    public static MeetingWindow? FindFrame(IReadOnlyList<MeetingWindow> windows)
    {
        MeetingWindow? frame = null;
        foreach (var window in windows)
        {
            var b = window.Bounds;
            if (window.Topmost && window.ClickThrough && b.Width >= MinFrameSize && b.Height >= MinFrameSize
                && (frame is null || Area(b) > Area(frame.Value.Bounds)))
            {
                frame = window;
            }
        }

        return frame;
    }

    /// <summary>A frame around a whole monitor is a screen share, anything else a window share.</summary>
    public static SharedArea Classify(PhysicalRect frame, IReadOnlyList<PhysicalRect> monitors)
    {
        foreach (var monitor in monitors)
        {
            if (Frames(frame, monitor))
            {
                return new SharedArea(ShareKind.Screen, monitor);
            }
        }

        return new SharedArea(ShareKind.Window, frame);
    }

    private static double Area(PhysicalRect r) => r.Width * r.Height;

    private static bool Frames(PhysicalRect frame, PhysicalRect monitor) =>
        Math.Abs(frame.X - monitor.X) <= ScreenTolerance
        && Math.Abs(frame.Y - monitor.Y) <= ScreenTolerance
        && Math.Abs(frame.Right - monitor.Right) <= ScreenTolerance
        && Math.Abs(frame.Bottom - monitor.Bottom) <= ScreenTolerance;
}
