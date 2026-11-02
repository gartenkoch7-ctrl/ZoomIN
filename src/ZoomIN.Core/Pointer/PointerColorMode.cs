namespace ZoomIN.Core.Pointer;

/// <summary>How the magnified pointer is coloured.</summary>
public enum PointerColorMode
{
    /// <summary>As Windows draws it.</summary>
    Original,

    /// <summary>Light parts take the chosen colour, dark outlines stay dark.</summary>
    Tint,

    /// <summary>Colours inverted: a white arrow becomes black with a white outline.</summary>
    Inverted,
}
