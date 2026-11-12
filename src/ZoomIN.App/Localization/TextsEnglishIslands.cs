namespace ZoomIN.App.Localization;

/// <summary>English texts of the islands (M19 B1).</summary>
internal static class TextsEnglishIslands
{
    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>
    {
        ["Act.Islands"] = "Islands on/off",
        ["Act.IslandEdit"] = "Edit islands",
        ["Act.IslandEdit.Hint"] = "All monitors briefly show 1×. Drag a rectangle = new island, drag an island = move, drag a corner = change the part shown, wheel = level, Del = delete, Esc = done. While the mode runs, clicks do not reach programs.",
        ["Islands.On"] = "Islands on",
        ["Islands.Off"] = "Islands off",
        ["Islands.Edit.On"] = "Edit islands: drag a rectangle, Esc ends",
        ["Islands.Edit.Off"] = "Edit islands off",
        ["Islands.Island"] = "Island {0} of {1}, {2}×",
        ["Islands.Full"] = "At most 4 islands. Delete one first.",
        ["Islands.Deleted"] = "Island deleted",
        ["Islands.Docked"] = "Islands cannot be edited in docked mode",
        ["Islands.NoInput"] = "Islands cannot be edited right now: ZoomIN cannot take the mouse clicks",
        ["Islands.Title"] = "Islands",
        ["Islands.Visible"] = "Show islands",
        ["Islands.Visible.Desc"] = "Fixed parts of the screen such as the clock or a chat stay magnified above the zoom (fullscreen). Clicks go through an island.",
        ["Islands.Edit"] = "Edit islands",
        ["Islands.Edit.Desc"] = "Drag a rectangle, move, drag a corner, wheel for the level, Del deletes, Esc ends.",
        ["Islands.Edit.Start"] = "Start editing",
        ["Islands.Empty"] = "No islands yet. \"Start editing\" and drag a rectangle.",
        ["Islands.Row"] = "Island {0}",
        ["Islands.Row.Desc"] = "Monitor {0} · {1} × {2} px · {3}×",
        ["Islands.Row.Missing"] = "Monitor not attached · {0} × {1} px · {2}×",
        ["Islands.RemoveFor"] = "Delete island {0}",
        ["Islands.StillFor"] = "Island {0} as still image",
        ["Islands.Live"] = "Live",
        ["Islands.Still"] = "Still image",
        ["Islands.Remove"] = "Delete",
        ["Islands.Note"] = "Islands belong to places on the screen, not to windows: when a window moves, the island shows what is there now.",
    };
}
