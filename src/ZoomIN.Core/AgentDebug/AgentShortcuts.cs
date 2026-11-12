namespace ZoomIN.Core.AgentDebug;

/// <summary>Names of <see cref="AgentShortcut"/> in the tool arguments ("toggle_zoom", …).</summary>
public static class AgentShortcuts
{
    private static readonly (string Name, AgentShortcut Shortcut)[] s_names =
    [
        ("toggle_zoom", AgentShortcut.ToggleZoom),
        ("zoom_in", AgentShortcut.ZoomIn),
        ("zoom_out", AgentShortcut.ZoomOut),
        ("fullscreen", AgentShortcut.Fullscreen),
        ("lens", AgentShortcut.Lens),
        ("docked", AgentShortcut.Docked),
        ("toggle_filter", AgentShortcut.ToggleFilter),
        ("next_filter", AgentShortcut.NextFilter),
        ("toggle_window_filter", AgentShortcut.ToggleWindowFilter),
        ("toggle_crosshair", AgentShortcut.ToggleCrosshair),
        ("toggle_tracking", AgentShortcut.ToggleTracking),
        ("overview", AgentShortcut.Overview),
        ("overview_escape", AgentShortcut.OverviewEscape),
    ];

    public static IReadOnlyList<string> Names { get; } = Array.ConvertAll(s_names, n => n.Name);

    public static string NameOf(AgentShortcut shortcut)
    {
        foreach (var (n, s) in s_names)
        {
            if (s == shortcut)
            {
                return n;
            }
        }

        return shortcut.ToString();
    }

    public static bool TryParse(string? name, out AgentShortcut shortcut)
    {
        foreach (var (n, s) in s_names)
        {
            if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase))
            {
                shortcut = s;
                return true;
            }
        }

        shortcut = default;
        return false;
    }
}
