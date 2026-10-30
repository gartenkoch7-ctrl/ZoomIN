using System.Runtime.InteropServices;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Rendering.Gpu;

/// <summary>Constant buffer of Magnify.hlsl. 112 bytes, matching the HLSL cbuffer layout.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct MagnifyConstants
{
    public const int SizeInBytes = 112;

    public readonly float SourceOffsetU;
    public readonly float SourceOffsetV;
    public readonly float SourceScaleU;
    public readonly float SourceScaleV;
    public readonly float InnerOffsetU;
    public readonly float InnerOffsetV;
    public readonly float InnerScaleU;
    public readonly float InnerScaleV;
    public readonly FrameColor Frame;
    public readonly float R0, R1, R2, OffsetR;
    public readonly float G0, G1, G2, OffsetG;
    public readonly float B0, B1, B2, OffsetB;
    public readonly float TextureWidth;
    public readonly float TextureHeight;
    public readonly uint SmoothingMode;
    public readonly float Padding;

    private MagnifyConstants(
        PhysicalRect source, PhysicalRect monitor, PhysicalRect target, PhysicalRect viewport,
        FrameColor frame, in ColorMatrix color, Smoothing smoothing)
    {
        SourceOffsetU = (float)((source.X - monitor.X) / monitor.Width);
        SourceOffsetV = (float)((source.Y - monitor.Y) / monitor.Height);
        SourceScaleU = (float)(source.Width / monitor.Width);
        SourceScaleV = (float)(source.Height / monitor.Height);
        InnerOffsetU = (float)((target.X - viewport.X) / viewport.Width);
        InnerOffsetV = (float)((target.Y - viewport.Y) / viewport.Height);
        InnerScaleU = (float)(target.Width / viewport.Width);
        InnerScaleV = (float)(target.Height / viewport.Height);
        Frame = frame;
        (R0, R1, R2, OffsetR) = (color.M00, color.M01, color.M02, color.OffsetR);
        (G0, G1, G2, OffsetG) = (color.M10, color.M11, color.M12, color.OffsetG);
        (B0, B1, B2, OffsetB) = (color.M20, color.M21, color.M22, color.OffsetB);
        TextureWidth = (float)monitor.Width;
        TextureHeight = (float)monitor.Height;
        SmoothingMode = (uint)smoothing;
        Padding = 0;
    }

    /// <summary>Magnifies <paramref name="source"/> onto the whole viewport, without frame or filter.</summary>
    public static MagnifyConstants FromSourceRect(PhysicalRect source, PhysicalRect monitor) =>
        new(source, monitor, monitor, monitor, default, ColorMatrix.Identity, Smoothing.Soft);

    /// <summary>Magnifies onto the whole viewport with colour matrix and smoothing.</summary>
    public static MagnifyConstants Fullscreen(PhysicalRect source, PhysicalRect monitor, in ColorMatrix color, Smoothing smoothing) =>
        new(source, monitor, monitor, monitor, default, color, smoothing);

    /// <summary>
    /// Magnifies <paramref name="source"/> into <paramref name="target"/>; the rest of
    /// <paramref name="viewport"/> (the frame) is filled with <paramref name="frame"/>.
    /// </summary>
    public static MagnifyConstants WithFrame(
        PhysicalRect source, PhysicalRect monitor, PhysicalRect target, PhysicalRect viewport, FrameColor frame) =>
        new(source, monitor, target, viewport, frame, ColorMatrix.Identity, Smoothing.Soft);

    public static MagnifyConstants WithFrame(
        PhysicalRect source, PhysicalRect monitor, PhysicalRect target, PhysicalRect viewport,
        FrameColor frame, in ColorMatrix color, Smoothing smoothing) =>
        new(source, monitor, target, viewport, frame, color, smoothing);
}
