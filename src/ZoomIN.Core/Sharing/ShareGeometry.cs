using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Sharing;

/// <summary>Share area per monitor; called per frame, so it works on structs only.</summary>
public static class ShareGeometry
{
    /// <summary>
    /// Part of the share area on the monitor, in physical desktop pixels; false if the area does
    /// not touch it (a window on another monitor, or no share at all).
    /// </summary>
    public static bool TryClip(PhysicalRect? area, PhysicalRect monitor, out PhysicalRect clipped)
    {
        clipped = default;
        if (area is not { } a)
        {
            return false;
        }

        var left = Math.Max(a.X, monitor.X);
        var top = Math.Max(a.Y, monitor.Y);
        var right = Math.Min(a.Right, monitor.Right);
        var bottom = Math.Min(a.Bottom, monitor.Bottom);
        if (right <= left || bottom <= top)
        {
            return false;
        }

        clipped = new PhysicalRect(left, top, right - left, bottom - top);
        return true;
    }
}
