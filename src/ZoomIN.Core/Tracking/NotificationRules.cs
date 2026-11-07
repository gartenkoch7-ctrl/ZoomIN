using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tracking;

/// <summary>
/// Tells a toast apart from other shell windows of the same kind (notification centre, calendar):
/// toasts are small and sit inside one monitor, the flyouts span most of its height.
/// </summary>
public static class NotificationRules
{
    private const double MaxWidthShare = 0.4;
    private const double MaxHeightShare = 0.6;

    public static bool LooksLikeToast(PhysicalRect window, PhysicalRect monitor) =>
        window.Width > 0
        && window.Height > 0
        && monitor.Contains(new PhysicalPoint(window.X + (window.Width / 2), window.Y + (window.Height / 2)))
        && window.Width <= monitor.Width * MaxWidthShare
        && window.Height <= monitor.Height * MaxHeightShare;
}
