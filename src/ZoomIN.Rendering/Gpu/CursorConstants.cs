using System.Runtime.InteropServices;

namespace ZoomIN.Rendering.Gpu;

/// <summary>Constant buffer of Cursor.hlsl. 64 bytes, matching the HLSL cbuffer layout.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct CursorConstants
{
    public const int SizeInBytes = 64;

    public readonly float InnerOffsetU;
    public readonly float InnerOffsetV;
    public readonly float InnerScaleU;
    public readonly float InnerScaleV;
    public readonly FrameColor Rim;
    public readonly float TexelU;
    public readonly float TexelV;
    public readonly float RimTexels;
    public readonly float RingThickness;
    public readonly FrameColor Ring;

    private CursorConstants(
        float marginU, float marginV, FrameColor rim, float texelU, float texelV, float rimTexels, float ringThickness, FrameColor ring)
    {
        InnerOffsetU = marginU;
        InnerOffsetV = marginV;
        InnerScaleU = 1 - (2 * marginU);
        InnerScaleV = 1 - (2 * marginV);
        Rim = rim;
        TexelU = texelU;
        TexelV = texelV;
        RimTexels = rimTexels;
        RingThickness = ringThickness;
        Ring = ring;
    }

    /// <param name="marginU">Rim room on the left/right, as a fraction of the viewport width.</param>
    /// <param name="marginV">Rim room on top/bottom, as a fraction of the viewport height.</param>
    /// <param name="width">Pointer image width in texels.</param>
    /// <param name="height">Pointer image height in texels.</param>
    /// <param name="rimTexels">Rim width in texels; 0 = no rim.</param>
    /// <param name="rim">Rim colour.</param>
    public static CursorConstants ForPointer(float marginU, float marginV, int width, int height, float rimTexels, FrameColor rim) =>
        new(marginU, marginV, rimTexels > 0 ? rim : default, 1f / width, 1f / height, rimTexels, 0, default);

    public static CursorConstants ForRing(float thickness, FrameColor color) =>
        new(0, 0, default, 0, 0, 0, thickness, color);

    /// <param name="thicknessU">Line width as a fraction of the rectangle width (≥ 0.5 fills it).</param>
    /// <param name="thicknessV">Line height as a fraction of the rectangle height.</param>
    /// <param name="color">Premultiplied colour.</param>
    public static CursorConstants ForRect(float thicknessU, float thicknessV, FrameColor color) =>
        new(0, 0, default, thicknessU, thicknessV, 0, 0, color);
}
