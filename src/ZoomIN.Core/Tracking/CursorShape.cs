namespace ZoomIN.Core.Tracking;

/// <summary>Image of a Windows mouse pointer, premultiplied BGRA, top-down rows.</summary>
/// <param name="Handle">Windows cursor handle; identifies the shape for caching.</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="HotspotX">Click point inside the image.</param>
/// <param name="HotspotY">Click point inside the image.</param>
/// <param name="Pixels">Width × Height × 4 bytes.</param>
/// <param name="BaseSize">
/// Width in screen pixels at pointer size 1×; 0 = <paramref name="Width"/>. Images loaded larger
/// (sharp standard pointers, pointers enlarged by Windows) are drawn smaller by this ratio.
/// </param>
public sealed record CursorShape(nint Handle, int Width, int Height, int HotspotX, int HotspotY, byte[] Pixels, int BaseSize = 0)
{
    /// <summary>Image pixels per screen pixel at pointer size 1×.</summary>
    public double Density => BaseSize > 0 ? (double)Width / BaseSize : 1.0;
}
