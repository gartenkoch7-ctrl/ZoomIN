namespace ZoomIN.App.Localization;

/// <summary>German texts of zoom while moving windows (G1, page "Zoom &amp; Modi").</summary>
internal static class TextsGermanWindowDrag
{
    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>
    {
        ["Drag.Title"] = "Beim Fenster-Verschieben",
        ["Drag.Action"] = "Zoom beim Ziehen",
        ["Drag.Action.Desc"] = "Solange Sie ein Fenster verschieben oder seine Größe ändern, sehen Sie auf dem Monitor unter dem Mauszeiger mehr vom Bildschirm und damit, wohin das Fenster kommt. Nach dem Loslassen gleitet der Zoom an der Mausposition zurück. Die Lupe blendet sich beim Ziehen aus, im Modus Angedockt bleibt alles unverändert. Die gespeicherte Stufe ändert sich nicht.",
        ["Drag.Lift"] = "Aufheben (1×)",
        ["Drag.Lower"] = "Absenken auf eine Stufe",
        ["Drag.Unchanged"] = "Unverändert",
        ["Drag.Level"] = "Stufe beim Ziehen",
        ["Drag.Level.Desc"] = "Nur ein Monitor, der stärker vergrößert ist, geht auf diese Stufe.",
        ["Drag.OnMove"] = "Beim Verschieben",
        ["Drag.OnMove.Desc"] = "Fenster an der Titelleiste ziehen oder mit Alt+Leertaste und „Verschieben“.",
        ["Drag.OnResize"] = "Beim Größe ändern",
        ["Drag.OnResize.Desc"] = "Fenster am Rand oder an einer Ecke ziehen.",
    };
}
