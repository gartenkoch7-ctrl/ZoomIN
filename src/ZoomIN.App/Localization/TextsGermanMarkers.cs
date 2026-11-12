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

        // E1: show conditions and hold key.
        ["MarkerShow"] = "Anzeigen",
        ["MarkerShow.Crosshair"] = "Fadenkreuz anzeigen",
        ["MarkerShow.Crosshair.Desc"] = "Wann das Fadenkreuz erscheint. In Ruhe: eine Sekunde ohne Mausbewegung und Tippen.",
        ["MarkerShow.FocusFrame"] = "Fokusrahmen anzeigen",
        ["MarkerShow.Caret"] = "Textcursor-Markierung anzeigen",
        ["MarkerShow.Desc"] = "Wann die Markierung erscheint. In Ruhe: eine Sekunde ohne Mausbewegung und Tippen. Die Haltetaste stellen Sie unter „Tastenkürzel“ ein.",
        ["Show.Always"] = "Immer",
        ["Show.Idle"] = "Nur in Ruhe",
        ["Show.Moving"] = "Nur in Bewegung",
        ["Show.HoldKey"] = "Nur bei gehaltener Taste",
        ["HoldKey.Group"] = "Markierungen",
        ["HoldKey"] = "Haltetaste für Markierungen",
        ["HoldKey.Desc"] = "Solange Sie diese Taste halten, erscheinen Fadenkreuz, Textcursor-Markierung und Fokusrahmen, die auf „Nur bei gehaltener Taste“ stehen. Die Taste wirkt in Programmen weiter wie immer – Alt allein loslassen öffnet daher in vielen Programmen das Menü. Gilt für alle Profile.",
        ["HoldKey.RightShift"] = "Umschalt rechts",
        ["HoldKey.LeftShift"] = "Umschalt links",
        ["HoldKey.RightCtrl"] = "Strg rechts",
        ["HoldKey.LeftCtrl"] = "Strg links",
        ["HoldKey.RightAlt"] = "Alt Gr (Alt rechts)",
        ["HoldKey.LeftAlt"] = "Alt links",
    };
}
