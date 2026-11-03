namespace ZoomIN.Setup.Localization;

/// <summary>German texts of the setup wizard (M9).</summary>
internal static class SetupTextsGerman
{
    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>
    {
        ["Title"] = "ZoomIN-Setup",
        ["Zoom.In"] = "Größer",
        ["Zoom.Out"] = "Kleiner",
        ["Zoom.Hint"] = "Strg+Plus/Minus oder Strg+Mausrad vergrößert dieses Fenster.",
        ["Back"] = "Zurück",
        ["Next"] = "Weiter",
        ["Install"] = "Installieren",
        ["Finish"] = "Fertig",
        ["Retry"] = "Erneut versuchen",
        ["Progress"] = "Schritt {0} von {1}",

        ["Step0.Title"] = "Willkommen bei ZoomIN",
        ["Step0.Text"] = "ZoomIN ist eine kostenlose Bildschirmlupe. Dieses Setup installiert ZoomIN für alle Benutzer dieses PCs. Dafür fragt Windows einmal nach Administratorrechten.",
        ["Language"] = "Sprache",

        ["Step1.Title"] = "Version wählen",
        ["Step1.Text"] = "Stabil enthält fertige Versionen. Experimentell enthält zusätzlich Release-Kandidaten und develop-Builds; diese sind ungetestet.",
        ["Channel"] = "Kanal",
        ["Stable"] = "Stabil",
        ["Experimental"] = "Experimentell",
        ["Versions"] = "Verfügbare Versionen",
        ["PreRelease"] = "Vorabversion",
        ["Loading"] = "Suche nach Versionen …",
        ["None"] = "Keine Versionen gefunden. Bitte die Internetverbindung prüfen.",
        ["Found"] = "{0} Versionen gefunden. Die neueste ist ausgewählt.",
        ["Refresh"] = "Erneut suchen",

        ["Step2.Title"] = "Einstellungen",
        ["Step2.Text"] = "Alles lässt sich später in ZoomIN ändern.",
        ["Autostart"] = "ZoomIN mit Windows starten",
        ["Desktop"] = "Verknüpfung auf dem Desktop",
        ["Updates"] = "Nach neuen Versionen suchen",
        ["SecureDesktop"] = "Auch auf dem Anmeldebildschirm, Sperrbildschirm und bei Admin-Abfragen vergrößern",
        ["Updates.Desc"] = "Beim Start und einmal täglich. Installiert wird nur auf Klick.",
        ["ZoomLevel"] = "Zoomstufe",
        ["Filter"] = "Farbfilter",
        ["Filter.Off"] = "Aus",
        ["Filter.Inverted"] = "Farben umkehren",
        ["Filter.Grayscale"] = "Graustufen",
        ["Filter.YellowOnBlack"] = "Gelb auf Schwarz",
        ["Filter.BlackOnYellow"] = "Schwarz auf Gelb",
        ["Filter.WhiteOnBlue"] = "Weiß auf Blau",

        ["Step3.Title"] = "Installation",
        ["Legacy"] = "Entferne die alte ZoomIN-Installation …",
        ["Downloading"] = "Lade {0} herunter …",
        ["Installing"] = "Installiere {0} … Bitte die Windows-Abfrage bestätigen.",
        ["Configuring"] = "Übernehme die Einstellungen …",
        ["Result.PackageMismatch"] = "Das heruntergeladene Paket ist beschädigt oder verändert und wurde verworfen.",
        ["Result.SignatureRejected"] = "Das Paket ist nicht von ZoomIN signiert und wurde nicht installiert.",
        ["Result.Declined"] = "Abgebrochen: Die Windows-Abfrage wurde abgelehnt. Es wurde nichts geändert.",
        ["Result.Failed"] = "Die Installation ist fehlgeschlagen. Details stehen in %LocalAppData%\\ZoomIN\\logs.",

        ["Step4.Title"] = "Fertig",
        ["Step4.Text"] = "ZoomIN {0} ist installiert. Zoom an/aus: Strg+Alt+Z. Größer und kleiner: Strg+Alt+Bild auf/ab oder Strg+Mausrad.",
        ["StartNow"] = "ZoomIN jetzt starten",
    };
}
