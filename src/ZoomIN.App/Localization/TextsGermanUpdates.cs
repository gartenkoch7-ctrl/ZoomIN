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
        ["Updates.Result.SignatureRejected"] = "Das Paket ist nicht von ZoomIN signiert und wurde nicht installiert.",
        ["Updates.Result.AlreadyActive"] = "Diese Version ist bereits installiert.",
        ["Updates.Result.Failed"] = "Die Installation ist fehlgeschlagen. Details stehen im Protokoll (Info & Status).",
    };
}
