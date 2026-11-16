namespace ZoomIN.Core.WindowFilters;

/// <summary>
/// The shortcut "filter for the active window on/off" (ADR-0020): a window that looks filtered
/// gets "None", any other gets the automatic filter. The result is a rule for app + window class.
/// </summary>
public static class WindowFilterToggle
{
    /// <param name="kind">App + class of the active window.</param>
    /// <param name="palette">The window's current palette index.</param>
    /// <param name="monitorFiltered">The monitor filter is on where the window is.</param>
    /// <param name="options">Current options (for the automatic filter).</param>
    public static WindowFilterRule Next(WindowKind kind, byte palette, bool monitorFiltered, WindowFilterOptions options)
    {
        var looksFiltered = WindowPalette.IsOwnFilter(palette) || (palette == WindowPalette.Monitor && monitorFiltered);
        var rule = looksFiltered
            ? WindowFilterRule.For(kind, WindowFilterChoice.None)
            : WindowFilterRule.For(kind, WindowFilterChoice.Filter, options.AutoFilter);

        // The shortcut changes the window's filter only; a title bar choice made on the page stays.
        return rule with { TitleBar = TitleBarResolver.ChoiceFor(kind, options) };
    }
}
