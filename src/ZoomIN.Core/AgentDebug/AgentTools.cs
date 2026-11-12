namespace ZoomIN.Core.AgentDebug;

/// <summary>
/// The tools of the KI-Debug MCP server (ADR-0027): names, descriptions and parameters. The MCP
/// role lists them, the running ZoomIN carries them out. Descriptions are written for an agent.
/// </summary>
public static class AgentTools
{
    public const string GetState = "get_state";
    public const string TakeSnapshot = "take_snapshot";
    public const string RunShortcut = "run_shortcut";

    public const string ImagesParameter = "images";
    public const string MonitorParameter = "monitor";
    public const string CropParameter = "crop";
    public const string MaxWidthParameter = "max_width";
    public const string ShortcutParameter = "shortcut";

    public static IReadOnlyList<string> ImageChoices { get; } = ["result", "original", "weights", "all"];

    public static IReadOnlyList<AgentTool> All { get; } =
    [
        new(
            GetState,
            "Current state of the running ZoomIN: version, monitors with bounds, zoom (on, mode, level per monitor), "
            + "monitor colour filters, the filter per window (options, remembered rules by app + window class) and the "
            + "window regions the overlay filters right now (rectangle -> filter, front to back), plus the app and "
            + "window class of the foreground window. Never contains window titles or other content.",
            []),
        new(
            TakeSnapshot,
            "Debug snapshot: what ZoomIN draws on each monitor that it currently draws on. The overlay is hidden from "
            + "screen captures, so this is the only way to see it. 'result' is the picture the user sees (filters, zoom, "
            + "pointer), 'original' the desktop underneath, 'weights' the smart inversion weights (white = inverted). "
            + "A monitor where ZoomIN draws nothing (no zoom, nothing filtered) returns no image. Also returns a text "
            + "with mode, zoom, filters and window regions of that frame.",
            [
                new(ImagesParameter, "string", "Which images to return; default 'result'.", ImageChoices),
                new(MonitorParameter, "string", "Only this monitor, e.g. 'DISPLAY1'; default all.", []),
                new(CropParameter, "string", "Only this part of each image: 'x,y,width,height' in pixels of the monitor (0,0 = its top left).", []),
                new(MaxWidthParameter, "integer", "Scale each image down to at most this width in pixels; default: no scaling.", []),
            ]),
        new(
            RunShortcut,
            "Triggers one of ZoomIN's shortcuts, with the same effect as the hotkey (also when no hotkey is assigned). "
            + "Zoom and filter changes act on the monitor under the mouse; toggle_window_filter acts on the foreground "
            + "window and is remembered for its app + window class. Zoom changes animate: wait about half a second "
            + "before a snapshot. overview acts like tapping Ctrl+Alt+O (1× with a frame; again: zoom in at the frame), "
            + "overview_escape like Esc during the overview.",
            [new(ShortcutParameter, "string", "The shortcut.", AgentShortcuts.Names, Required: true)]),
    ];
}
