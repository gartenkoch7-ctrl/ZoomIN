namespace ZoomIN.Core.Filters;

/// <summary>Colour vision deficiencies a scheme corrects for (correction only, no simulation).</summary>
public enum ColorDeficiency
{
    /// <summary>Red–green, red cones weak or missing.</summary>
    Protan,

    /// <summary>Red–green, green cones weak or missing (the most common one).</summary>
    Deutan,

    /// <summary>Blue–yellow, blue cones weak or missing.</summary>
    Tritan,
}
