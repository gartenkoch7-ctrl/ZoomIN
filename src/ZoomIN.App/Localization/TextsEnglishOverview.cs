namespace ZoomIN.App.Localization;

/// <summary>English texts of the overview at the push of a key (M17 B2).</summary>
internal static class TextsEnglishOverview
{
    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>
    {
        ["Act.Overview"] = "Overview",
        ["Act.Overview.Hint"] = "Tap: the monitor briefly shows everything at 1×, a frame marks the previous view and follows the mouse. Tap again to zoom in there, Esc returns to the previous place. Hold: overview while the shortcut is held; releasing zooms in at the frame. Works in fullscreen and lens mode.",
        ["Overview.ViewOverview.Desc"] = "At a high level, Ctrl+Alt+O briefly shows the whole screen – with a frame where you zoom back in.",
        ["ViewOverview.On"] = "Overview on",
        ["ViewOverview.Off"] = "Overview off",
    };
}
