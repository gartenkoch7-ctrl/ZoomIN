using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Modes;

/// <summary>
/// Inverse of <see cref="ModeGeometry"/>: finds the fixpoint that puts a desktop point in the
/// middle of the magnified view. The fixpoint itself is shown where it is on the desktop
/// (ADR-0003), so anchoring the view directly at a focus near the edge would show it near the
/// edge. Clamped like the geometry: the view stops at the monitor edge. No allocations.
/// </summary>
public static class ViewCentering
{
    public static PhysicalPoint AnchorFor(
        ZoomMode mode, PhysicalRect monitor, PhysicalPoint center, double zoom, LensSize lens, DockLayout dock)
    {
        // Docked: the source is already centred on the (clamped) anchor.
        if (mode == ZoomMode.Docked)
        {
            return center;
        }

        // Fullscreen is the lens that covers the whole monitor.
        var width = mode == ZoomMode.Lens ? monitor.Width * lens.WidthFraction : monitor.Width;
        var height = mode == ZoomMode.Lens ? monitor.Height * lens.HeightFraction : monitor.Height;
        var z = Math.Max(zoom, ViewportCalculator.MinZoom);
        return new PhysicalPoint(
            Axis(center.X, monitor.X, monitor.Right, width, z),
            Axis(center.Y, monitor.Y, monitor.Bottom, height, z));
    }

    // While the target (lens) is centred on the anchor p, the source is centred on p as well.
    // Once the target stops at the edge (centre t), the source centre is p + (t − p)/z;
    // solved for p it gives the formulas below.
    private static double Axis(double c, double min, double max, double size, double z)
    {
        var keep = 1 - (1 / z);
        var low = min + (size / 2);
        var high = max - (size / 2);
        if (keep <= 1e-9)
        {
            return Math.Clamp(c, min, max);
        }

        if (c < low)
        {
            return Math.Max((c - (low / z)) / keep, min);
        }

        if (c > high)
        {
            return Math.Min((c - (high / z)) / keep, max);
        }

        return c;
    }
}
