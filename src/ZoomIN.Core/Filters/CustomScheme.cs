namespace ZoomIN.Core.Filters;

/// <summary>
/// An own, named colour scheme (M18, C1–C3). The definitions are shared by all profiles; which
/// filter is chosen belongs to the profile. Colours are "#RRGGBB" like the pointer colours.
/// Properties use <c>set</c> for the JSON source generator (see the remark on AppSettings); treat
/// instances as immutable and change them with <c>with</c>.
/// </summary>
public sealed record CustomScheme
{
    public const int MaxNameLength = 40;
    public const int MaxStrength = 100;
    public const int MaxReplacements = 3;

    /// <summary>The place, <see cref="FilterId.Custom1"/> to <see cref="FilterId.Custom4"/>; rules and profiles name it.</summary>
    public FilterId Id { get; set; } = FilterId.Custom1;

    /// <summary>Chosen by the user; never in telemetry (AGENTS.md 2.5).</summary>
    public string Name { get; set; } = string.Empty;

    public SchemeKind Kind { get; set; }

    /// <summary><see cref="SchemeKind.TwoColors"/>: what light parts become.</summary>
    public string Color1 { get; set; } = "#FFD600";

    /// <summary><see cref="SchemeKind.TwoColors"/>: what dark parts become.</summary>
    public string Color2 { get; set; } = "#000000";

    /// <summary><see cref="SchemeKind.Tint"/>: the hue laid over the picture.</summary>
    public string Hue { get; set; } = "#FFC107";

    public ColorDeficiency Deficiency { get; set; } = ColorDeficiency.Deutan;

    /// <summary><see cref="SchemeKind.Deficiency"/>: 0–100 %, 0 leaves the colours unchanged.</summary>
    public int Strength { get; set; } = MaxStrength;

    /// <summary>Up to <see cref="MaxReplacements"/>, applied with every kind.</summary>
    public IReadOnlyList<ColorReplacement> Replacements { get; set; } = [];
}
