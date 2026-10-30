namespace ZoomIN.Core.Tracking;

/// <summary>Image of a Windows mouse pointer, premultiplied BGRA, top-down rows.</summary>
/// <param name="Handle">Windows cursor handle; identifies the shape for caching.</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="HotspotX">Click point inside the image.</param>
/// <param name="HotspotY">Click point inside the image.</param>
/// <param name="Pixels">Width × Height × 4 bytes.</param>
public sealed record CursorShape(nint Handle, int Width, int Height, int HotspotX, int HotspotY, byte[] Pixels);
