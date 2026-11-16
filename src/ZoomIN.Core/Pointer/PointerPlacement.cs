using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Pointer;

/// <summary>
/// Where the magnified pointer image is drawn. Its hotspot stays exactly on the click point
/// (ADR-0003), so near the bottom/right monitor edge the big image would hang off the screen
/// and almost vanish. Then it is mirrored on that axis: the arrow points into the screen and the
/// tip still marks the click point. Pure math per frame, no allocations.
/// </summary>
/// <param name="X">Left edge of the image box (rim included).</param>
/// <param name="Y">Top edge of the image box.</param>
/// <param name="FlipX">Mirrored left/right.</param>
/// <param name="FlipY">Mirrored top/bottom.</param>
public readonly record struct PointerPlacement(double X, double Y, bool FlipX, bool FlipY)
{
    // Flip once more than this share of the image would be cut off. Lower while already flipped
    // (hysteresis), so a mouse resting on the boundary does not make the pointer flicker.
    private const double FlipShare = 1.0 / 8;
    private const double KeepFlippedShare = 1.0 / 16;

    /// <param name="tip">Where the hotspot is shown.</param>
    /// <param name="width">Image box width in screen pixels.</param>
    /// <param name="height">Image box height in screen pixels.</param>
    /// <param name="hotspotX">Hotspot inside the unmirrored box.</param>
    /// <param name="hotspotY">Hotspot inside the unmirrored box.</param>
    /// <param name="area">The screen area the pointer must stay on (the monitor).</param>
    /// <param name="previous">Last frame's placement, for the hysteresis.</param>
    public static PointerPlacement Place(
        PhysicalPoint tip, double width, double height, double hotspotX, double hotspotY, PhysicalRect area, PointerPlacement previous)
    {
        var flipX = Flip(tip.X, width, hotspotX, area.X, area.Right, previous.FlipX);
        var flipY = Flip(tip.Y, height, hotspotY, area.Y, area.Bottom, previous.FlipY);
        return new PointerPlacement(
            tip.X - (flipX ? width - hotspotX : hotspotX),
            tip.Y - (flipY ? height - hotspotY : hotspotY),
            flipX,
            flipY);
    }

    private static bool Flip(double tip, double size, double hotspot, double low, double high, bool wasFlipped)
    {
        var normal = Overflow(tip - hotspot, size, low, high);
        var mirrored = Overflow(tip - (size - hotspot), size, low, high);
        var threshold = size * (wasFlipped ? KeepFlippedShare : FlipShare);
        return normal > threshold && mirrored < normal;
    }

    private static double Overflow(double start, double size, double low, double high) =>
        Math.Max(0, low - start) + Math.Max(0, start + size - high);
}
