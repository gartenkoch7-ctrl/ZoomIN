namespace ZoomIN.App.Localization;

/// <summary>German texts of mouse buffer and axis lock (A6, page "Mauszeiger").</summary>
internal static class TextsGermanPointerConstraint
{
    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>
    {
        ["Constraint.Title"] = "Bewegung begrenzen",
        ["Constraint.Buffer"] = "Maus-Puffer am Monitorrand",
        ["Constraint.Buffer.Desc"] = "Der Mauszeiger bleibt am Rand eines Monitors stehen und rutscht nicht versehentlich auf den nächsten. Erst wer eine Weile gegen den Rand drückt, kommt hinüber. Gilt nur zwischen Monitoren, nicht am Rand der Lupe.",
        ["Constraint.Buffer.Off"] = "Aus",
        ["Constraint.Buffer.Light"] = "Leicht (kurz drücken, 0,2 Sekunden)",
        ["Constraint.Buffer.Medium"] = "Mittel (0,4 Sekunden drücken)",
        ["Constraint.Buffer.Strong"] = "Stark (lange drücken, 0,8 Sekunden)",
        ["Constraint.Axis"] = "Achsensperre",
        ["Constraint.Axis.Desc"] = "Solange Sie die Haltetaste gedrückt halten, bewegt sich der Mauszeiger nur waagrecht oder nur senkrecht, zum Beispiel entlang einer Tabellenzeile. Die erste Bewegung entscheidet die Richtung. Beim Loslassen ist der Zeiger sofort wieder frei.",
        ["Constraint.Key"] = "Haltetaste für die Achsensperre",
        ["Constraint.Key.Desc"] = "Hinweis: Umschalt und Ziehen markiert in vielen Programmen Text, Alt öffnet beim Loslassen oft das Menü. Wählen Sie eine Taste, die Sie sonst selten allein halten.",
        ["Constraint.Key.LeftShift"] = "Umschalt links",
        ["Constraint.Key.RightShift"] = "Umschalt rechts",
        ["Constraint.Key.LeftControl"] = "Strg links",
        ["Constraint.Key.RightControl"] = "Strg rechts",
        ["Constraint.Key.LeftAlt"] = "Alt links",
        ["Constraint.Key.RightAlt"] = "Alt Gr (Alt rechts)",
    };
}
