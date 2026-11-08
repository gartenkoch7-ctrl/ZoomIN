namespace ZoomIN.Core.Pointer;

/// <summary>How the magnified pointer is coloured. The numbers are passed to Cursor.hlsl.</summary>
public enum PointerColorMode
{
    /// <summary>As Windows draws it.</summary>
    Original,

    /// <summary>Light parts take the chosen colour, dark outlines stay dark.</summary>
    Tint,

    /// <summary>Colours inverted: a white arrow becomes black with a white outline.</summary>
    Inverted,

    /// <summary>
    /// Follows the background (M15): on dark ground the body takes one colour of a pair, on light
    /// ground the other; the outline always takes the opposite one.
    /// </summary>
    Adaptive,
}
