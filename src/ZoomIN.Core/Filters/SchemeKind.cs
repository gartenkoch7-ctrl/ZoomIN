namespace ZoomIN.Core.Filters;

/// <summary>How an own scheme (<see cref="CustomScheme"/>) colours the picture (M18, C1–C3).</summary>
public enum SchemeKind
{
    /// <summary>Light becomes colour 1, dark becomes colour 2, everything in between a blend.</summary>
    TwoColors,

    /// <summary>One hue over everything: white becomes the hue, black stays black, lighter stays lighter (luma × hue).</summary>
    Tint,

    /// <summary>Correction for a colour vision deficiency, with a strength.</summary>
    Deficiency,

    /// <summary>Original colours; only the replacements apply.</summary>
    ReplaceOnly,
}
