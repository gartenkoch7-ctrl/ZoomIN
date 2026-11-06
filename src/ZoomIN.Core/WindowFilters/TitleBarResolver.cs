using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.WindowFilters;

/// <summary>
/// Palette of the title bar strip of each window (see <see cref="WindowTitleBarChoice"/>): only a
/// window with a filter of its own gets a separate title bar region, and only when the title bar
/// should look different from the window – then it shows the monitor filter like a dark window.
/// </summary>
public static class TitleBarResolver
{
    /// <summary>The title bar shows what its window shows; no extra region.</summary>
    public const byte Same = byte.MaxValue;

    /// <summary>One entry per window, <see cref="Same"/> or a palette index for its title bar strip.</summary>
    public static byte[] Resolve(IReadOnlyList<TrackedWindow> frontToBack, byte[] palette, WindowFilterOptions options)
    {
        var titleBars = new byte[frontToBack.Count];
        for (var i = 0; i < titleBars.Length; i++)
        {
            titleBars[i] = Resolve(frontToBack[i], palette[i], options);
        }

        return titleBars;
    }

    /// <summary>The title bar palette of one window that got <paramref name="windowPalette"/>.</summary>
    public static byte Resolve(in TrackedWindow window, byte windowPalette, WindowFilterOptions options)
    {
        if (!options.Enabled || window.Role != WindowRole.Normal || window.TitleBarHeight <= 0
            || !WindowPalette.IsOwnFilter(windowPalette))
        {
            return Same;
        }

        return ChoiceFor(window.Kind, options) switch
        {
            WindowTitleBarChoice.Never => WindowPalette.Monitor,
            WindowTitleBarChoice.LikeWindow => Same,

            // Unknown or not measured (e.g. a fixed filter of the user): like the window, as before.
            _ => window.TitleBarBrightness == WindowBrightness.Dark ? WindowPalette.Monitor : Same,
        };
    }

    /// <summary>
    /// A window with a fixed filter of the user (also from the shortcut) needs no look for itself,
    /// but its title bar does: without one, "automatic" would invert a dark title bar after a
    /// restart. Looked at whatever the title bar choice is, so changing it later works at once.
    /// </summary>
    public static bool NeedsMeasurement(WindowKind kind, WindowRole role, WindowFilterOptions options) =>
        options.Enabled && role == WindowRole.Normal
        && WindowFilterResolver.ChoiceFor(kind, role, options, out _) == WindowFilterChoice.Filter;

    /// <summary>The user's title bar choice for app + window class; automatic without a rule.</summary>
    public static WindowTitleBarChoice ChoiceFor(WindowKind kind, WindowFilterOptions options) =>
        options.RuleFor(kind)?.TitleBar ?? WindowTitleBarChoice.Automatic;

    /// <summary>The title bar strip at the top of the window's visible frame.</summary>
    public static PhysicalRect Area(in TrackedWindow window) => Area(window.Bounds, window.TitleBarHeight);

    /// <summary>The top <paramref name="height"/> pixels of <paramref name="bounds"/> (empty for 0).</summary>
    public static PhysicalRect Area(PhysicalRect bounds, int height) =>
        new(bounds.X, bounds.Y, bounds.Width, Math.Clamp(height, 0, bounds.Height));
}
