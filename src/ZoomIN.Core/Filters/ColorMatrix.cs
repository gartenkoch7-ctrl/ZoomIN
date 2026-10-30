namespace ZoomIN.Core.Filters;

/// <summary>
/// Linear colour transform: out = M · rgb + offset (alpha untouched). All filters, contrast and
/// brightness compose into one matrix, so the shader cost is the same for every filter.
/// </summary>
public readonly record struct ColorMatrix(
    float M00, float M01, float M02,
    float M10, float M11, float M12,
    float M20, float M21, float M22,
    float OffsetR, float OffsetG, float OffsetB)
{
    public static ColorMatrix Identity { get; } = new(1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0);

    /// <summary>Applies this matrix to a colour (0–1 per channel), clamped like the shader does.</summary>
    public (float R, float G, float B) Apply(float r, float g, float b) => (
        Math.Clamp((M00 * r) + (M01 * g) + (M02 * b) + OffsetR, 0, 1),
        Math.Clamp((M10 * r) + (M11 * g) + (M12 * b) + OffsetG, 0, 1),
        Math.Clamp((M20 * r) + (M21 * g) + (M22 * b) + OffsetB, 0, 1));

    /// <summary>Returns "apply <paramref name="first"/>, then this".</summary>
    public ColorMatrix After(ColorMatrix first) => new(
        (M00 * first.M00) + (M01 * first.M10) + (M02 * first.M20),
        (M00 * first.M01) + (M01 * first.M11) + (M02 * first.M21),
        (M00 * first.M02) + (M01 * first.M12) + (M02 * first.M22),
        (M10 * first.M00) + (M11 * first.M10) + (M12 * first.M20),
        (M10 * first.M01) + (M11 * first.M11) + (M12 * first.M21),
        (M10 * first.M02) + (M11 * first.M12) + (M12 * first.M22),
        (M20 * first.M00) + (M21 * first.M10) + (M22 * first.M20),
        (M20 * first.M01) + (M21 * first.M11) + (M22 * first.M21),
        (M20 * first.M02) + (M21 * first.M12) + (M22 * first.M22),
        (M00 * first.OffsetR) + (M01 * first.OffsetG) + (M02 * first.OffsetB) + OffsetR,
        (M10 * first.OffsetR) + (M11 * first.OffsetG) + (M12 * first.OffsetB) + OffsetG,
        (M20 * first.OffsetR) + (M21 * first.OffsetG) + (M22 * first.OffsetB) + OffsetB);
}
