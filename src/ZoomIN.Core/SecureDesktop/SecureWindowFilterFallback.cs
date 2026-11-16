using ZoomIN.Core.Filters;
using ZoomIN.Core.Settings;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Core.SecureDesktop;

/// <summary>
/// The filter per window does not run on the secure desktop (no window tracking there, ADR-0020).
/// A user who relies on it alone would see the sign-in, lock and UAC screens unfiltered, so there its
/// automatic filter becomes the monitor filter wherever the user has no monitor filter switched on
/// (decision 2026-11-16, M10 test). Smart inversion leaves the dark lock-screen picture alone.
/// Monitor filters the user switched on stay as they are. Pure, no IO.
/// </summary>
public static class SecureWindowFilterFallback
{
    /// <param name="settings">Already sanitised settings of the secure instance.</param>
    /// <param name="userWindowFilter">The filter per window as the user's file has it (before sanitising).</param>
    public static AppSettings Apply(AppSettings settings, WindowFilterOptions? userWindowFilter)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (userWindowFilter is not { Enabled: true })
        {
            return settings;
        }

        // Normalised like the tray app does in memory: an old file's factory "Inverted" becomes the
        // smart inversion there too, and an unknown value falls back to it.
        var auto = userWindowFilter.Normalized().AutoFilter;
        var monitors = new Dictionary<string, MonitorFilter>(settings.MonitorFilters.Count, StringComparer.Ordinal);
        foreach (var (id, own) in settings.MonitorFilters)
        {
            monitors[id] = own.Enabled ? own : new MonitorFilter(true, auto);
        }

        return settings with
        {
            FilterEnabled = true,
            ColorFilter = settings.FilterEnabled ? settings.ColorFilter : auto,
            MonitorFilters = monitors,
        };
    }
}
