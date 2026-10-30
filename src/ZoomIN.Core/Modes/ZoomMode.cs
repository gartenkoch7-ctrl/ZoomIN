namespace ZoomIN.Core.Modes;

/// <summary>How the magnified image is shown (docs/kb/produkt-und-scope.md).</summary>
public enum ZoomMode
{
    /// <summary>The whole monitor is magnified.</summary>
    Fullscreen,

    /// <summary>A rectangle around the mouse pointer is magnified.</summary>
    Lens,

    /// <summary>A reserved strip at one monitor edge shows the area around the pointer.</summary>
    Docked,
}
