using ZoomIN.Core.Pointer;

namespace ZoomIN.Rendering.Gpu;

/// <summary>Premultiplied RGBA colour for the shaders (frames, rim, locator, crosshair).</summary>
internal readonly record struct FrameColor(float R, float G, float B, float A)
{
    /// <summary>Amber: high contrast on light and dark content (lens and docked frame).</summary>
    public static FrameColor Default { get; } = new(1.0f, 0.76f, 0.03f, 1.0f);

    /// <summary>Premultiplies a settings colour with <paramref name="alpha"/>.</summary>
    public static FrameColor From(RgbColor color, double alpha = 1.0)
    {
        var a = (float)Math.Clamp(alpha, 0, 1);
        return new FrameColor(color.R / 255f * a, color.G / 255f * a, color.B / 255f * a, a);
    }
}
