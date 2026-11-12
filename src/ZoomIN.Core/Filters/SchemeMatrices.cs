using ZoomIN.Core.Pointer;

namespace ZoomIN.Core.Filters;

/// <summary>
/// Colour matrices of the own schemes (M18, C1–C3). The replacements are no matrix; they go to the
/// shader as their own parameters (<see cref="ReplacementTable"/>). Derivation: docs/kb/farbschemata.md.
/// </summary>
public static class SchemeMatrices
{
    // Rec. 709 luma weights, shared with the built-in filters.
    private const float Lr = ColorFilters.Lr;
    private const float Lg = ColorFilters.Lg;
    private const float Lb = ColorFilters.Lb;

    // Simulation of full dichromacy (severity 1.0) after Machado, Oliveira, Fernandes 2009,
    // "A Physiologically-based Model for Simulation of Color Vision Deficiency" (IEEE TVCG 15(6)).
    private static readonly ColorMatrix s_protan = new(
        0.152286f, 1.052583f, -0.204868f,
        0.114503f, 0.786281f, 0.099216f,
        -0.003882f, -0.048116f, 1.051998f,
        0, 0, 0);

    private static readonly ColorMatrix s_deutan = new(
        0.367322f, 0.860646f, -0.227968f,
        0.280085f, 0.672501f, 0.047413f,
        -0.011820f, 0.042940f, 0.968881f,
        0, 0, 0);

    private static readonly ColorMatrix s_tritan = new(
        1.255528f, -0.076749f, -0.178779f,
        -0.078411f, 0.930809f, 0.147602f,
        0.004733f, 0.691367f, 0.303900f,
        0, 0, 0);

    // Where the lost difference goes (daltonisation after Fidaner, Lin, Ozguven 2005): a red–green
    // error is moved into green and blue, a blue–yellow error into red and green.
    private static readonly ColorMatrix s_shiftRedGreen = new(0, 0, 0, 0.7f, 1, 0, 0.7f, 0, 1, 0, 0, 0);
    private static readonly ColorMatrix s_shiftBlueYellow = new(1, 0, 0.7f, 0, 1, 0.7f, 0, 0, 0, 0, 0, 0);

    /// <summary>The matrix of <paramref name="scheme"/>, without contrast and brightness.</summary>
    public static ColorMatrix For(CustomScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        return scheme.Kind switch
        {
            SchemeKind.TwoColors => TwoColors(Color(scheme.Color1), Color(scheme.Color2)),
            SchemeKind.Tint => Tint(Color(scheme.Hue)),
            SchemeKind.Deficiency => Correction(scheme.Deficiency, scheme.Strength / (float)CustomScheme.MaxStrength),
            _ => ColorMatrix.Identity,
        };
    }

    /// <summary>Luma as an affine ramp: c2 + L·(c1 − c2); white becomes c1, black c2.</summary>
    public static ColorMatrix TwoColors((float R, float G, float B) light, (float R, float G, float B) dark)
    {
        var (dr, dg, db) = (light.R - dark.R, light.G - dark.G, light.B - dark.B);
        return new ColorMatrix(
            dr * Lr, dr * Lg, dr * Lb,
            dg * Lr, dg * Lg, dg * Lb,
            db * Lr, db * Lg, db * Lb,
            dark.R, dark.G, dark.B);
    }

    /// <summary>Luma times the hue: white becomes the hue, black stays black.</summary>
    public static ColorMatrix Tint((float R, float G, float B) hue) => TwoColors(hue, (0, 0, 0));

    /// <summary>
    /// Daltonisation I + S·(I − Sim): what the eye loses is added back in channels it still sees.
    /// Mixed with the identity by <paramref name="strength"/> (0–1), so 0 changes nothing. Grey
    /// stays grey, since each simulation row sums to 1.
    /// </summary>
    public static ColorMatrix Correction(ColorDeficiency deficiency, float strength)
    {
        var (simulation, shift) = deficiency switch
        {
            ColorDeficiency.Protan => (s_protan, s_shiftRedGreen),
            ColorDeficiency.Tritan => (s_tritan, s_shiftBlueYellow),
            _ => (s_deutan, s_shiftRedGreen),
        };

        var identity = ColorMatrix.Identity;
        var lost = identity.Plus(simulation.Scaled(-1));
        var correction = shift.After(lost);
        return identity.Plus(correction.Scaled(Math.Clamp(strength, 0, 1)));
    }

    private static (float R, float G, float B) Color(string hex) => RgbColor.ParseOrDefault(hex, RgbColor.White).ToUnit();
}
