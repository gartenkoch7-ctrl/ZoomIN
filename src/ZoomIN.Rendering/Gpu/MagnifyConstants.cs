using System.Runtime.InteropServices;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Rendering.Gpu;

/// <summary>Constant buffer of Magnify.hlsl. 176 bytes, matching the HLSL cbuffer layout.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct MagnifyConstants
{
    public const int SizeInBytes = 176;

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
    public readonly float RegionMinU;
    public readonly float RegionMinV;
    public readonly float RegionMaxU;
    public readonly float RegionMaxV;
    public readonly float RegionR0, RegionR1, RegionR2, RegionOffsetR;
    public readonly float RegionG0, RegionG1, RegionG2, RegionOffsetG;
    public readonly float RegionB0, RegionB1, RegionB2, RegionOffsetB;

    private MagnifyConstants(
        PhysicalRect source, PhysicalRect monitor, PhysicalRect target, PhysicalRect viewport,
        FrameColor frame, in ColorMatrix color, Smoothing smoothing, ColorRegion? region)
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

        // Left at zero (an empty rectangle) without a region: every pixel gets the monitor's matrix.
        if (region is { } r)
        {
            var a = r.Area;
            RegionMinU = (float)((a.X - monitor.X) / monitor.Width);
            RegionMinV = (float)((a.Y - monitor.Y) / monitor.Height);
            RegionMaxU = (float)((a.Right - monitor.X) / monitor.Width);
            RegionMaxV = (float)((a.Bottom - monitor.Y) / monitor.Height);
            var m = r.Matrix;
            (RegionR0, RegionR1, RegionR2, RegionOffsetR) = (m.M00, m.M01, m.M02, m.OffsetR);
            (RegionG0, RegionG1, RegionG2, RegionOffsetG) = (m.M10, m.M11, m.M12, m.OffsetG);
            (RegionB0, RegionB1, RegionB2, RegionOffsetB) = (m.M20, m.M21, m.M22, m.OffsetB);
        }
    }

    /// <summary>Magnifies <paramref name="source"/> onto the whole viewport, without frame or filter.</summary>
    public static MagnifyConstants FromSourceRect(PhysicalRect source, PhysicalRect monitor) =>
        new(source, monitor, monitor, monitor, default, ColorMatrix.Identity, Smoothing.Soft, null);

    /// <summary>
    /// Magnifies onto the whole viewport with colour matrix and smoothing; inside
    /// <paramref name="region"/> the region's matrix replaces <paramref name="color"/>.
    /// </summary>
    public static MagnifyConstants Fullscreen(
        PhysicalRect source, PhysicalRect monitor, in ColorMatrix color, Smoothing smoothing, ColorRegion? region = null) =>
        new(source, monitor, monitor, monitor, default, color, smoothing, region);

    /// <summary>
    /// Magnifies <paramref name="source"/> into <paramref name="target"/>; the rest of
    /// <paramref name="viewport"/> (the frame) is filled with <paramref name="frame"/>.
    /// </summary>
    public static MagnifyConstants WithFrame(
        PhysicalRect source, PhysicalRect monitor, PhysicalRect target, PhysicalRect viewport, FrameColor frame) =>
        new(source, monitor, target, viewport, frame, ColorMatrix.Identity, Smoothing.Soft, null);

    public static MagnifyConstants WithFrame(
        PhysicalRect source, PhysicalRect monitor, PhysicalRect target, PhysicalRect viewport,
        FrameColor frame, in ColorMatrix color, Smoothing smoothing, ColorRegion? region = null) =>
        new(source, monitor, target, viewport, frame, color, smoothing, region);
}
