using ZoomIN.Core.Filters;
using ZoomIN.Core.Settings;
using ZoomIN.Core.Updates;

namespace ZoomIN.Setup.Install;

/// <summary>What the user picked in the wizard (ADR-0014).</summary>
/// <param name="Channel">Release channel for later updates.</param>
/// <param name="Autostart">Start ZoomIN with Windows.</param>
/// <param name="DesktopShortcut">Shortcut on the user's desktop.</param>
/// <param name="CheckForUpdates">Check at start-up and daily.</param>
/// <param name="ZoomLevel">Fullscreen zoom level to start with.</param>
/// <param name="Filter">Colour filter switched on from the start, or null for none.</param>
internal sealed record SetupChoices(
    ReleaseChannel Channel, bool Autostart, bool DesktopShortcut, bool CheckForUpdates, double ZoomLevel, ColorFilter? Filter)
{
    /// <summary>
    /// Merges the choices into existing settings (a reinstall keeps everything else) and marks the
    /// first-run assistant as done, because the setup covered it (decision Q13).
    /// </summary>
    public AppSettings ApplyTo(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings with
        {
            UpdateChannel = Channel,
            CheckForUpdates = CheckForUpdates,
            LastZoomLevel = ZoomLevel,
            FilterEnabled = Filter is not null,
            ColorFilter = Filter ?? settings.ColorFilter,
            FirstRunCompleted = true,
        };
    }
}
