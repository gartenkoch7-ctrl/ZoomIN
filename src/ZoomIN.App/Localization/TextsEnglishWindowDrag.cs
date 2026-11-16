namespace ZoomIN.App.Localization;

/// <summary>English texts of zoom while moving windows (G1, page "Zoom &amp; modes").</summary>
internal static class TextsEnglishWindowDrag
{
    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>
    {
        ["Drag.Title"] = "While moving windows",
        ["Drag.Action"] = "Zoom while dragging",
        ["Drag.Action.Desc"] = "While you move or resize a window, the monitor under the pointer shows more of the screen, so you see where the window goes. When you let go, the zoom glides back at the mouse position. The lens hides while dragging; docked mode stays unchanged. The saved level does not change.",
        ["Drag.Lift"] = "Lift (1×)",
        ["Drag.Lower"] = "Lower to a level",
        ["Drag.Unchanged"] = "Unchanged",
        ["Drag.Level"] = "Level while dragging",
        ["Drag.Level.Desc"] = "Only a monitor zoomed in further goes to this level.",
        ["Drag.OnMove"] = "When moving",
        ["Drag.OnMove.Desc"] = "Drag a window by its title bar, or use Alt+Space and \"Move\".",
        ["Drag.OnResize"] = "When resizing",
        ["Drag.OnResize.Desc"] = "Drag a window by its border or a corner.",
    };
}
