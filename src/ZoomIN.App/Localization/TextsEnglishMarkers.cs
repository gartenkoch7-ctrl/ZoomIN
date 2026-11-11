namespace ZoomIN.App.Localization;

/// <summary>English texts of the highlights: caret shapes (E2).</summary>
internal static class TextsEnglishMarkers
{
    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>
    {
        ["CaretMarker.Group"] = "Text cursor mark",
        ["CaretMarker.Preview"] = "Preview: {0} in {1}, {2}",
        ["CaretMarker.PreviewOff"] = "Preview: no mark (Mark the text cursor is off)",
        ["CaretMarker.Shape"] = "Shape",
        ["CaretMarker.Shape.Desc"] = "How the typing position is marked in the magnified view.",
        ["CaretMarker.Color"] = "Colour",
        ["CaretMarker.Thickness"] = "Thickness",
        ["CaretMarker.Thickness.Desc"] = "Grows with the zoom so the mark always stays easy to see.",
        ["CaretShape.Bar"] = "Bar",
        ["CaretShape.Wedges"] = "Wedges above and below the cursor",
        ["CaretShape.Frame"] = "Frame around the character",
        ["CaretShape.Circle"] = "Circle",
        ["CaretThickness.Thin"] = "Thin",
        ["CaretThickness.Medium"] = "Medium",
        ["CaretThickness.Thick"] = "Thick",
    };
}
