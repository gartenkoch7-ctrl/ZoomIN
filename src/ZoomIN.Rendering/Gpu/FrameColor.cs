namespace ZoomIN.Rendering.Gpu;

/// <summary>Premultiplied RGBA colour of the lens/docked frame.</summary>
internal readonly record struct FrameColor(float R, float G, float B, float A)
{
    /// <summary>Amber: high contrast on light and dark content. Configurable in M5/M7.</summary>
    public static FrameColor Default { get; } = new(1.0f, 0.76f, 0.03f, 1.0f);
}
