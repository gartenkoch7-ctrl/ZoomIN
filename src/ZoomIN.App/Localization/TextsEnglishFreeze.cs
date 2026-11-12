namespace ZoomIN.App.Localization;

/// <summary>English texts of frozen monitors and the monitor switch (G2, M19).</summary>
internal static class TextsEnglishFreeze
{
    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>
    {
        ["Act.FreezeMonitor"] = "Freeze monitor / unfreeze all",
        ["Act.FreezeMonitor.Hint"] = "The monitor under the pointer keeps its view, the pointer jumps to the nearest free monitor and cannot go back until you press again. Windows cannot be dragged onto the frozen monitor either.",
        ["Act.NextMonitor"] = "Pointer to next monitor",
        ["Freeze.Frozen"] = "Monitor {0} frozen",
        ["Freeze.Lifted"] = "Monitors unfrozen",
        ["Freeze.OneMonitor"] = "Freezing needs at least two monitors.",
        ["Freeze.LastFree"] = "The last free monitor cannot be frozen.",
        ["Freeze.NoPointer"] = "The pointer is not on any monitor.",
        ["Freeze.MoveRefused"] = "Windows refused to move the pointer.",
        ["Freeze.Title"] = "Monitors",
        ["Switch.Done"] = "Monitor {0}",
        ["Switch.OneMonitor"] = "There is no other monitor.",
        ["Switch.NoFree"] = "There is no other free monitor.",
        ["Freeze.Group"] = "Frozen monitors",
        ["Freeze.Content"] = "Content",
        ["Freeze.Content.Desc"] = "Ctrl+Alt+H freezes the monitor under the pointer. \"Live\" keeps the view moving (a video keeps playing), \"Still image\" keeps the picture of that moment. After a restart nothing is frozen.",
        ["Freeze.Live"] = "Live",
        ["Freeze.Still"] = "Still image",
        ["Freeze.Color"] = "Frame colour",
        ["Freeze.Color.Desc"] = "A frozen monitor gets a frame in this colour.",
    };
}
