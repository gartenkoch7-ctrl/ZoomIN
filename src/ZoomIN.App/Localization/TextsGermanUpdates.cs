namespace ZoomIN.App.Localization;

/// <summary>German texts of the updates page (M9).</summary>
internal static class TextsGermanUpdates
{
    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>
    {
        ["NavUpdates"] = "Updates",
        ["Updates.Installed"] = "Installierte Version",
        ["Updates.Channel"] = "Kanal",
        ["Updates.Channel.Desc"] = "Experimentell enthält Release-Kandidaten und develop-Builds. Sie sind ungetestet und können Fehler haben.",
        ["Updates.Stable"] = "Stabil",
        ["Updates.Experimental"] = "Experimentell",
        ["Updates.Versions"] = "Verfügbare Versionen",
        ["Updates.Refresh"] = "Jetzt suchen",
        ["Updates.PreRelease"] = "Vorabversion",
        ["Updates.Upgrade"] = "Auf {0} aktualisieren",
        ["Updates.Downgrade"] = "Zu {0} zurückgehen",
        ["Updates.Current"] = "{0} ist installiert",
        ["Updates.DowngradeHint"] = "Vor dem Zurückgehen werden Einstellungen und Profile gesichert.",
        ["Updates.NotInstalled"] = "Dieses ZoomIN wurde nicht mit dem Setup installiert (Entwickler-Build oder portables ZIP). Updates aus der App gibt es nur für installiertes ZoomIN.",
        ["Updates.Loading"] = "Suche nach Versionen …",
        ["Updates.None"] = "Keine Versionen gefunden. Ohne Internet oder vor dem ersten Release ist die Liste leer.",
        ["Updates.UpToDate"] = "ZoomIN ist aktuell.",
        ["Updates.Available"] = "Version {0} ist verfügbar.",
        ["Updates.Downloading"] = "Lade {0} herunter …",
        ["Updates.Installing"] = "Installiere {0} … Bitte die Windows-Abfrage bestätigen.",
        ["Updates.Result.Installed"] = "{0} ist installiert. ZoomIN startet neu …",
        ["Updates.Result.Declined"] = "Abgebrochen: Die Windows-Abfrage wurde abgelehnt. Es wurde nichts geändert.",
        ["Updates.Result.PackageMismatch"] = "Das heruntergeladene Paket ist beschädigt oder verändert und wurde verworfen.",
        ["Updates.Result.SignatureRejected"] = "Das Paket trägt eine beschädigte oder fremde Signatur und wurde nicht installiert.",
        ["Updates.Result.AlreadyActive"] = "Diese Version ist bereits installiert.",
        ["Updates.Result.Failed"] = "Die Installation ist fehlgeschlagen. Details stehen im Protokoll (Info & Status).",

        ["Uninstall.Title"] = "ZoomIN deinstallieren",
        ["Uninstall.Question"] = "ZoomIN wird von diesem PC entfernt.\n\nSollen deine Einstellungen und Profile erhalten bleiben?\n\nJa: Einstellungen behalten\nNein: alles löschen\nAbbrechen: nichts ändern",
        ["Uninstall.Done"] = "ZoomIN wurde entfernt.",
        ["Uninstall.Failed"] = "ZoomIN wurde nicht entfernt. Die Windows-Abfrage wurde abgelehnt oder die Entfernung ist fehlgeschlagen.",

        ["UiAccess.Title"] = "Zoom liegt nicht über allen Menüs",
        ["UiAccess.Tray"] = "Taskleiste, Ton-, Netzwerk- und Startmenü bleiben unvergrößert sichtbar. Öffne ZoomIN, um es richtig zu installieren.",
        ["UiAccess.Banner"] = "Diese ZoomIN-Version darf nicht über Taskleiste, Schnelleinstellungen (Ton, Netzwerk, Bluetooth), Startmenü und Info-Center zeichnen – sie bleiben unvergrößert sichtbar. Das kann nur die mit dem Setup installierte Version.",
        ["UiAccess.Install"] = "ZoomIN installieren",
        ["UiAccess.Downloading"] = "Lade das Setup herunter und prüfe es …",
        ["UiAccess.Result.Started"] = "Das Setup wurde gestartet. Bitte die Windows-Abfrage bestätigen.",
        ["UiAccess.Result.NoRelease"] = "Es gibt noch keine Version mit Setup in deinem Update-Kanal.",
        ["UiAccess.Result.DownloadFailed"] = "Das Setup konnte nicht geladen werden. Bitte die Internetverbindung prüfen.",
        ["UiAccess.Result.NotTrusted"] = "Das heruntergeladene Setup trägt eine beschädigte oder fremde Signatur und wurde nicht gestartet.",
    };
}
