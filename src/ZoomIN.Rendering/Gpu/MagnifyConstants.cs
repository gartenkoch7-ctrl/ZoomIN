using System.Runtime.InteropServices;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Rendering.Gpu;

/// <summary>Constant buffer of Magnify.hlsl. 48 bytes, matching the HLSL cbuffer layout.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct MagnifyConstants
{
    public const int SizeInBytes = 48;

    public readonly float SourceOffsetU;
    public readonly float SourceOffsetV;
    public readonly float SourceScaleU;
    public readonly float SourceScaleV;
    public readonly float InnerOffsetU;
    public readonly float InnerOffsetV;
    public readonly float InnerScaleU;
    public readonly float InnerScaleV;
    public readonly float FrameR;
    public readonly float FrameG;
    public readonly float FrameB;
    public readonly float FrameA;

    private MagnifyConstants(PhysicalRect source, PhysicalRect monitor, PhysicalRect target, PhysicalRect viewport, FrameColor frame)
    {
        SourceOffsetU = (float)((source.X - monitor.X) / monitor.Width);
        SourceOffsetV = (float)((source.Y - monitor.Y) / monitor.Height);
        SourceScaleU = (float)(source.Width / monitor.Width);
        SourceScaleV = (float)(source.Height / monitor.Height);
        InnerOffsetU = (float)((target.X - viewport.X) / viewport.Width);
        InnerOffsetV = (float)((target.Y - viewport.Y) / viewport.Height);
        InnerScaleU = (float)(target.Width / viewport.Width);
        InnerScaleV = (float)(target.Height / viewport.Height);
        FrameR = frame.R;
        FrameG = frame.G;
        FrameB = frame.B;
        FrameA = frame.A;
    }

    /// <summary>Magnifies <paramref name="source"/> onto the whole viewport, without a frame.</summary>
    public static MagnifyConstants FromSourceRect(PhysicalRect source, PhysicalRect monitor) =>
        new(source, monitor, monitor, monitor, default);

    /// <summary>
    /// Magnifies <paramref name="source"/> into <paramref name="target"/>; the rest of
    /// <paramref name="viewport"/> (the frame) is filled with <paramref name="frame"/>.
    /// </summary>
    public static MagnifyConstants WithFrame(
        PhysicalRect source, PhysicalRect monitor, PhysicalRect target, PhysicalRect viewport, FrameColor frame) =>
        new(source, monitor, target, viewport, frame);
}
