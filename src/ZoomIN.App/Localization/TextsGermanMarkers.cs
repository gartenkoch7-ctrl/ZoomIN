namespace ZoomIN.App.Localization;

/// <summary>German texts of the highlights: caret shapes (E2).</summary>
internal static class TextsGermanMarkers
{
    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>
    {
        ["CaretMarker.Group"] = "Textcursor-Markierung",
        ["CaretMarker.Preview"] = "Vorschau: {0} in {1}, {2}",
        ["CaretMarker.PreviewOff"] = "Vorschau: keine Markierung (Textcursor markieren ist aus)",
        ["CaretMarker.Shape"] = "Form",
        ["CaretMarker.Shape.Desc"] = "Wie die Schreibposition in der Vergrößerung markiert wird.",
        ["CaretMarker.Color"] = "Farbe",
        ["CaretMarker.Thickness"] = "Dicke",
        ["CaretMarker.Thickness.Desc"] = "Wächst mit dem Zoom, damit die Markierung immer gut zu sehen ist.",
        ["CaretShape.Bar"] = "Balken",
        ["CaretShape.Wedges"] = "Keile über und unter dem Cursor",
        ["CaretShape.Frame"] = "Rahmen um das Zeichen",
        ["CaretShape.Circle"] = "Kreis",
        ["CaretThickness.Thin"] = "Dünn",
        ["CaretThickness.Medium"] = "Mittel",
        ["CaretThickness.Thick"] = "Dick",
    };
}
