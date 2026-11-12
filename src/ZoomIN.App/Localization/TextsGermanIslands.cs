namespace ZoomIN.App.Localization;

/// <summary>Deutsche Texte der Inseln (M19 B1).</summary>
internal static class TextsGermanIslands
{
    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>
    {
        ["Act.Islands"] = "Inseln ein/aus",
        ["Act.IslandEdit"] = "Inseln bearbeiten",
        ["Act.IslandEdit.Hint"] = "Alle Monitore zeigen kurz 1×. Rechteck aufziehen = neue Insel, Insel ziehen = verschieben, Ecke ziehen = Ausschnitt ändern, Mausrad = Stufe, Entf = löschen, Esc = fertig. Solange der Modus läuft, gehen Klicks nicht an Programme.",
        ["Islands.On"] = "Inseln an",
        ["Islands.Off"] = "Inseln aus",
        ["Islands.Edit.On"] = "Inseln bearbeiten: Rechteck aufziehen, Esc beendet",
        ["Islands.Edit.Off"] = "Inseln bearbeiten aus",
        ["Islands.Island"] = "Insel {0} von {1}, {2}-fach",
        ["Islands.Full"] = "Höchstens 4 Inseln. Erst eine löschen.",
        ["Islands.Deleted"] = "Insel gelöscht",
        ["Islands.Docked"] = "Inseln lassen sich im angedockten Modus nicht bearbeiten",
        ["Islands.Title"] = "Inseln",
        ["Islands.Visible"] = "Inseln anzeigen",
        ["Islands.Visible.Desc"] = "Feste Bildschirmbereiche wie Uhr oder Chat bleiben vergrößert über dem Zoom stehen (Vollbild). Klicks gehen durch die Insel hindurch. Strg+Alt+I",
        ["Islands.Edit"] = "Inseln bearbeiten",
        ["Islands.Edit.Desc"] = "Rechteck aufziehen, verschieben, Ecke ziehen, Mausrad für die Stufe, Entf löscht, Esc beendet. Strg+Alt+Umschalt+I",
        ["Islands.Edit.Start"] = "Bearbeiten starten",
        ["Islands.Empty"] = "Noch keine Inseln. „Bearbeiten starten“ und ein Rechteck aufziehen.",
        ["Islands.Row"] = "Insel {0}",
        ["Islands.Row.Desc"] = "Monitor {0} · {1} × {2} px · {3}-fach",
        ["Islands.Row.Missing"] = "Monitor nicht angeschlossen · {0} × {1} px · {2}-fach",
        ["Islands.RemoveFor"] = "Insel {0} löschen",
        ["Islands.StillFor"] = "Insel {0} als Standbild",
        ["Islands.Live"] = "Live",
        ["Islands.Still"] = "Standbild",
        ["Islands.Remove"] = "Löschen",
        ["Islands.Note"] = "Inseln hängen an Bildschirmstellen, nicht an Fenstern: Wird ein Fenster verschoben, zeigt die Insel, was jetzt dort liegt.",
    };
}
