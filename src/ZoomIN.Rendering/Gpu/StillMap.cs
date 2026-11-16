using ZoomIN.Core.Viewport;

namespace ZoomIN.Rendering.Gpu;

/// <summary>
/// Maps desktop UV to the UV of the texture Magnify.hlsl samples (M19 B1): the desktop itself
/// (<see cref="Live"/>), or a still image that holds only a part of it (<see cref="For"/>).
/// </summary>
internal readonly record struct StillMap(float OffsetU, float OffsetV, float ScaleU, float ScaleV)
{
    /// <summary>The bound texture is the whole desktop: coordinates unchanged.</summary>
    public static StillMap Live { get; } = new(0, 0, 1, 1);

    /// <summary>The bound texture holds <paramref name="part"/> (texture pixels) of a <paramref name="width"/> × <paramref name="height"/> desktop.</summary>
    public static StillMap For(PhysicalRect part, double width, double height) => new(
        (float)(-part.X / part.Width), (float)(-part.Y / part.Height), (float)(width / part.Width), (float)(height / part.Height));
}
