namespace ZoomIN.Core.Filters;

/// <summary>One colour replacement as the shader takes it (see <see cref="ReplacementTable"/>).</summary>
/// <param name="Y">Luma of the colour to catch.</param>
/// <param name="Cb">Blue difference of the colour to catch.</param>
/// <param name="Cr">Red difference of the colour to catch.</param>
/// <param name="Radius">Distance in YCbCr up to which colours are caught; the soft edge starts at half of it.</param>
/// <param name="ShiftR">New minus old colour, red (−1 to 1).</param>
/// <param name="ShiftG">New minus old colour, green.</param>
/// <param name="ShiftB">New minus old colour, blue.</param>
public readonly record struct PackedReplacement(float Y, float Cb, float Cr, float Radius, float ShiftR, float ShiftG, float ShiftB);
