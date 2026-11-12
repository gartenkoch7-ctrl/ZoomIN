namespace ZoomIN.App.Localization;

/// <summary>German texts of the overview at the push of a key (M17 B2).</summary>
internal static class TextsGermanOverview
{
    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>
    {
        ["Act.Overview"] = "Übersicht",
        ["Act.Overview.Hint"] = "Antippen: Der Monitor zeigt kurz alles in 1×, ein Rahmen markiert den bisherigen Ausschnitt und folgt der Maus. Erneut antippen zoomt dort hinein, Esc kehrt zur alten Stelle zurück. Gehalten: Übersicht, solange das Kürzel gedrückt ist; Loslassen zoomt am Rahmen hinein. Wirkt in Vollbild und Lupe.",
        ["Overview.ViewOverview.Desc"] = "Strg+Alt+O zeigt bei hoher Stufe kurz den ganzen Bildschirm – mit einem Rahmen, an dem Sie wieder hineinzoomen.",
        ["ViewOverview.On"] = "Übersicht an",
        ["ViewOverview.Off"] = "Übersicht aus",
    };
}
