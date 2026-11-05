using System.Runtime.InteropServices;

namespace ZoomIN.Rendering.Gpu;

/// <summary>Constant buffer of Cursor.hlsl. 80 bytes, matching the HLSL cbuffer layout.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct CursorConstants
{
    public const int SizeInBytes = 80;

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

    /// <summary>rgb = tint colour, a = colour mode (0 original, 1 tint, 2 inverted).</summary>
    public readonly FrameColor Tint;

    private CursorConstants(
        float marginU, float marginV, FrameColor rim, float texelU, float texelV, float rimTexels, float ringThickness, FrameColor ring,
        FrameColor tint, bool flipU = false, bool flipV = false)
    {
        // Mirroring needs no shader change: the shader computes uv = (Uv − offset) / scale, so
        // starting at the far margin with a negative scale yields 1 − uv.
        InnerOffsetU = flipU ? 1 - marginU : marginU;
        InnerOffsetV = flipV ? 1 - marginV : marginV;
        InnerScaleU = (flipU ? -1 : 1) * (1 - (2 * marginU));
        InnerScaleV = (flipV ? -1 : 1) * (1 - (2 * marginV));
        Rim = rim;
        TexelU = texelU;
        TexelV = texelV;
        RimTexels = rimTexels;
        RingThickness = ringThickness;
        Ring = ring;
        Tint = tint;
    }

    /// <param name="marginU">Rim room on the left/right, as a fraction of the viewport width.</param>
    /// <param name="marginV">Rim room on top/bottom, as a fraction of the viewport height.</param>
    /// <param name="width">Pointer image width in texels.</param>
    /// <param name="height">Pointer image height in texels.</param>
    /// <param name="rimTexels">Rim width in texels; 0 = no rim.</param>
    /// <param name="rim">Rim colour.</param>
    /// <param name="tint">Colour mode, see <see cref="Tint"/>.</param>
    /// <param name="flipU">Mirror the image left/right (pointer at the right monitor edge).</param>
    /// <param name="flipV">Mirror the image top/bottom (pointer at the bottom monitor edge).</param>
    public static CursorConstants ForPointer(
        float marginU, float marginV, int width, int height, float rimTexels, FrameColor rim, FrameColor tint,
        bool flipU = false, bool flipV = false) =>
        new(marginU, marginV, rimTexels > 0 ? rim : default, 1f / width, 1f / height, rimTexels, 0, default, tint, flipU, flipV);

    public static CursorConstants ForRing(float thickness, FrameColor color) =>
        new(0, 0, default, 0, 0, 0, thickness, color, default);

    /// <param name="thicknessU">Line width as a fraction of the rectangle width (≥ 0.5 fills it).</param>
    /// <param name="thicknessV">Line height as a fraction of the rectangle height.</param>
    /// <param name="color">Premultiplied colour.</param>
    public static CursorConstants ForRect(float thicknessU, float thicknessV, FrameColor color) =>
        new(0, 0, default, thicknessU, thicknessV, 0, 0, color, default);
}
