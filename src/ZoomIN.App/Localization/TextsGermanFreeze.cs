namespace ZoomIN.App.Localization;

/// <summary>German texts of frozen monitors and the monitor switch (G2, M19).</summary>
internal static class TextsGermanFreeze
{
    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>
    {
        ["Act.FreezeMonitor"] = "Monitor einfrieren / alle aufheben",
        ["Act.FreezeMonitor.Hint"] = "Der Monitor unter dem Mauszeiger behält seinen Ausschnitt, der Zeiger springt auf den nächsten freien Monitor und kann nicht zurück, bis Sie erneut drücken. Fenster lassen sich so auch nicht auf den eingefrorenen Monitor ziehen.",
        ["Act.NextMonitor"] = "Zeiger auf nächsten Monitor",
        ["Freeze.Frozen"] = "Monitor {0} eingefroren",
        ["Freeze.Lifted"] = "Einfrieren aufgehoben",
        ["Freeze.OneMonitor"] = "Einfrieren braucht mindestens zwei Monitore.",
        ["Freeze.Overview"] = "Erst die Übersicht beenden, dann einfrieren.",
        ["Freeze.Docked"] = "Der angedockte Monitor lässt sich nicht einfrieren.",
        ["Freeze.NoPointer"] = "Der Mauszeiger ist auf keinem Monitor.",
        ["Freeze.LayoutUnavailable"] = "Die Monitore ändern sich gerade. Bitte gleich noch einmal versuchen.",
        ["Freeze.MoveRefused"] = "Windows hat das Versetzen des Mauszeigers abgelehnt.",
        ["Freeze.Title"] = "Monitore",
        ["Switch.Done"] = "Monitor {0}",
        ["Switch.OneMonitor"] = "Es gibt keinen weiteren Monitor.",
        ["Switch.NoFree"] = "Es gibt keinen weiteren freien Monitor.",
        ["Freeze.Group"] = "Eingefrorene Monitore",
        ["Freeze.Content"] = "Inhalt",
        ["Freeze.Content.Desc"] = "Strg+Alt+H friert den Monitor unter dem Mauszeiger ein. „Live“ zeigt den Ausschnitt weiter bewegt (ein Video läuft weiter), „Standbild“ hält das Bild des Augenblicks fest. Nach einem Neustart ist nichts eingefroren.",
        ["Freeze.Live"] = "Live",
        ["Freeze.Still"] = "Standbild",
        ["Freeze.Color"] = "Rahmenfarbe",
        ["Freeze.Color.Desc"] = "Ein eingefrorener Monitor bekommt einen Rahmen in dieser Farbe.",
    };
}
