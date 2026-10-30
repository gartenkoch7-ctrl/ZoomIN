using System.Runtime.InteropServices;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Rendering.Gpu;

/// <summary>Constant buffer of Magnify.hlsl. 16 bytes, matching the HLSL cbuffer layout.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct MagnifyConstants
{
    public readonly float OffsetU;
    public readonly float OffsetV;
    public readonly float ScaleU;
    public readonly float ScaleV;

    public MagnifyConstants(float offsetU, float offsetV, float scaleU, float scaleV)
    {
        OffsetU = offsetU;
        OffsetV = offsetV;
        ScaleU = scaleU;
        ScaleV = scaleV;
    }

    /// <summary>Converts a desktop source rectangle into UV space of the captured monitor texture.</summary>
    public static MagnifyConstants FromSourceRect(PhysicalRect source, PhysicalRect monitor) =>
        new(
            (float)((source.X - monitor.X) / monitor.Width),
            (float)((source.Y - monitor.Y) / monitor.Height),
            (float)(source.Width / monitor.Width),
            (float)(source.Height / monitor.Height));
}
