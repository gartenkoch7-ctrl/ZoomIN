using System.Globalization;

namespace ZoomIN.Core.Viewport;

/// <summary>
/// One spelling of a rectangle for diagnostics texts – snapshot info and KI-Debug state (ADR-0027) –
/// so an agent can compare them: "(x,y widthxheight)", invariant culture.
/// </summary>
public static class RectText
{
    public static string Format(PhysicalRect r) =>
        string.Create(CultureInfo.InvariantCulture, $"({r.X:0},{r.Y:0} {r.Width:0}x{r.Height:0})");
}
