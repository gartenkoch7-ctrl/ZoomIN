namespace ZoomIN.Core.WindowFilters;

/// <summary>
/// Order of ADR-0020 for one window: user choice → automatic → monitor filter. A window with a
/// filter of its own is left out of the monitor filter, so it is never filtered twice.
/// </summary>
public static class WindowFilterResolver
{
    /// <summary>The choice that applies: the user's rule, else the preset; the Windows interface is always automatic.</summary>
    public static WindowFilterChoice ChoiceFor(WindowKind kind, WindowRole role, WindowFilterOptions options, out WindowFilterRule? rule)
    {
        rule = null;
        if (role == WindowRole.Shell)
        {
            return WindowFilterChoice.Automatic;
        }

        rule = options.RuleFor(kind);
        return rule?.Choice ?? WindowFilterPresets.ChoiceWithoutRule(kind);
    }

    /// <summary>Palette index for a normal window or a shell window (popups take their owner's).</summary>
    public static byte Resolve(WindowKind kind, WindowRole role, WindowBrightness brightness, WindowFilterOptions options)
    {
        if (!options.Enabled)
        {
            return WindowPalette.Monitor;
        }

        return ChoiceFor(kind, role, options, out var rule) switch
        {
            WindowFilterChoice.None => WindowPalette.Original,
            WindowFilterChoice.Filter => WindowPalette.For(rule!.Filter),
            _ => role != WindowRole.Shell && brightness == WindowBrightness.Light
                ? WindowPalette.For(options.AutoFilter)
                : WindowPalette.Monitor,
        };
    }

    /// <summary>Only normal windows on "automatic" are measured; everything else is known without looking.</summary>
    public static bool NeedsMeasurement(WindowKind kind, WindowRole role, WindowFilterOptions options) =>
        options.Enabled && role == WindowRole.Normal
        && ChoiceFor(kind, role, options, out _) == WindowFilterChoice.Automatic;
}
