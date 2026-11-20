namespace ZoomIN.Core.WindowFilters;

/// <summary>
/// What the colour filter shortcut (Ctrl+Alt+C by default) switches (ADR-0033). With the smart inversion on,
/// switching the whole monitor would also invert every dark window again, so one press switches
/// only the active window (remembered per app, like the window filter shortcut of ADR-0020) and two
/// quick presses switch the whole monitor. Every other filter switches the monitor as before.
/// </summary>
public static class FilterShortcutTarget
{
    /// <summary>A second press within this time switches the whole monitor and undoes the first.</summary>
    public static readonly TimeSpan DoublePress = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// True when one press acts on the active window: the monitor shows the smart inversion and the
    /// filter per window is on (its rules have no effect otherwise).
    /// </summary>
    public static bool ActsOnWindow(bool monitorIsSmart, bool windowFilterEnabled) => monitorIsSmart && windowFilterEnabled;

    /// <summary>The second press came soon enough after the first to count as a double press.</summary>
    public static bool IsDoublePress(TimeSpan sinceFirst) => sinceFirst >= TimeSpan.Zero && sinceFirst <= DoublePress;

    /// <summary>
    /// The rule one press gives the window on a smart monitor: original colours ("None") while it shows
    /// a filter, back to "Automatic" while it shows original colours. Unlike the window filter shortcut
    /// it never sets a fixed filter, so the window follows the monitor's smart inversion again.
    /// </summary>
    /// <param name="palette">The window's current palette index (used when the app has no rule).</param>
    public static WindowFilterRule NextRule(WindowKind kind, byte palette, WindowFilterOptions options)
    {
        // The rule decides first: it changes at once, while the tracker publishes the new palette a
        // little later, so a quick second press would otherwise set "None" again.
        var rule = options.RuleFor(kind);
        var showsOriginal = rule is null ? palette == WindowPalette.Original : rule.Choice == WindowFilterChoice.None;
        var choice = showsOriginal ? WindowFilterChoice.Automatic : WindowFilterChoice.None;

        // The shortcut changes the choice only; title bar and filter of a rule made on the page stay.
        return (rule ?? WindowFilterRule.For(kind, choice)) with { Choice = choice };
    }
}
