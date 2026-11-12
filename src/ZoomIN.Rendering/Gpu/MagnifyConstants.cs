using System.Runtime.InteropServices;
using ZoomIN.Core.Filters;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Rendering.Gpu;

/// <summary>Constant buffer of Magnify.hlsl. 144 bytes, matching the HLSL cbuffer layout.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct MagnifyConstants
{
    public const int SizeInBytes = 144;

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
    public readonly uint MonitorSmart;
    public readonly float Sharpness;
    public readonly float BoldPixels;
    public readonly StillMap Still;
    private readonly float _stillPadding0;
    private readonly float _stillPadding1;

    private MagnifyConstants(
        PhysicalRect source, PhysicalRect monitor, PhysicalRect target, PhysicalRect viewport,
        FrameColor frame, in ColorMatrix color, Smoothing smoothing, bool smart = false, TextEnhancement enhance = default,
        StillMap? still = null)
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
        MonitorSmart = smart ? 1u : 0u;
        // PSEnhanced always samples bicubic: with another smoothing it would switch it silently (risk 3).
        var applied = TextEnhancement.WorksWith(smoothing) ? enhance : TextEnhancement.Off;
        Sharpness = (float)applied.Sharpness;
        BoldPixels = (float)applied.BoldPixels;
        Still = still ?? StillMap.Live;
        _stillPadding0 = 0;
        _stillPadding1 = 0;
    }

    /// <summary>"Schärfe" or "Fett" is on: <see cref="MagnifyPass"/> binds PSEnhanced instead of PSMain (M18, D1).</summary>
    public bool Enhanced => Sharpness > 0 || BoldPixels > 0;

    /// <summary>Magnifies <paramref name="source"/> onto the whole viewport, without frame or filter.</summary>
    public static MagnifyConstants FromSourceRect(PhysicalRect source, PhysicalRect monitor) =>
        new(source, monitor, monitor, monitor, default, ColorMatrix.Identity, Smoothing.Soft);

    /// <summary>Magnifies onto the whole viewport with colour matrix and smoothing.</summary>
    /// <param name="smart">The matrix is the inverted part of the smart inversion (ADR-0024).</param>
    /// <param name="enhance">"Schärfe"/"Fett" as <see cref="TextEnhancement.At(double, Smoothing)"/> resolved them for this zoom.</param>
    public static MagnifyConstants Fullscreen(
        PhysicalRect source, PhysicalRect monitor, in ColorMatrix color, Smoothing smoothing, bool smart = false, TextEnhancement enhance = default) =>
        new(source, monitor, monitor, monitor, default, color, smoothing, smart, enhance);

    /// <summary>
    /// Magnifies <paramref name="source"/> into <paramref name="target"/>; the rest of
    /// <paramref name="viewport"/> (the frame) is filled with <paramref name="frame"/>.
    /// </summary>
    public static MagnifyConstants WithFrame(
        PhysicalRect source, PhysicalRect monitor, PhysicalRect target, PhysicalRect viewport, FrameColor frame) =>
        new(source, monitor, target, viewport, frame, ColorMatrix.Identity, Smoothing.Soft);

    public static MagnifyConstants WithFrame(
        PhysicalRect source, PhysicalRect monitor, PhysicalRect target, PhysicalRect viewport,
        FrameColor frame, in ColorMatrix color, Smoothing smoothing, bool smart = false, TextEnhancement enhance = default) =>
        new(source, monitor, target, viewport, frame, color, smoothing, smart, enhance);

    /// <summary>
    /// An island (M19 B1): like <see cref="WithFrame(PhysicalRect, PhysicalRect, PhysicalRect, PhysicalRect, FrameColor, in ColorMatrix, Smoothing, bool, TextEnhancement)"/>,
    /// and with <paramref name="still"/> the bound texture is a still image of part of the desktop.
    /// </summary>
    public static MagnifyConstants ForIsland(
        PhysicalRect source, PhysicalRect monitor, PhysicalRect target, PhysicalRect viewport,
        FrameColor frame, in ColorMatrix color, Smoothing smoothing, bool smart, TextEnhancement enhance, StillMap still) =>
        new(source, monitor, target, viewport, frame, color, smoothing, smart, enhance, still);
}
