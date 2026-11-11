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

        // E1: show conditions and hold key.
        ["MarkerShow"] = "Show",
        ["MarkerShow.Crosshair"] = "Show crosshair",
        ["MarkerShow.Crosshair.Desc"] = "When the crosshair appears. At rest: one second without moving the mouse or typing.",
        ["MarkerShow.FocusFrame"] = "Show focus frame",
        ["MarkerShow.Caret"] = "Show text cursor mark",
        ["MarkerShow.Desc"] = "When the mark appears. At rest: one second without moving the mouse or typing. Set the hold key under “Shortcuts”.",
        ["Show.Always"] = "Always",
        ["Show.Idle"] = "Only at rest",
        ["Show.Moving"] = "Only while moving",
        ["Show.HoldKey"] = "Only while a key is held",
        ["HoldKey.Group"] = "Highlights",
        ["HoldKey"] = "Hold key for highlights",
        ["HoldKey.Desc"] = "While you hold this key, crosshair, text cursor mark and focus frame set to “Only while a key is held” appear. Programs still get the key as usual, so releasing Alt on its own opens the menu in many programs. Applies to all profiles.",
        ["HoldKey.RightShift"] = "Right Shift",
        ["HoldKey.LeftShift"] = "Left Shift",
        ["HoldKey.RightCtrl"] = "Right Ctrl",
        ["HoldKey.LeftCtrl"] = "Left Ctrl",
        ["HoldKey.RightAlt"] = "Alt Gr (right Alt)",
        ["HoldKey.LeftAlt"] = "Left Alt",
    };
}
