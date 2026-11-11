using Microsoft.Extensions.Logging;
using ZoomIN.App.Settings;
using ZoomIN.App.Updates;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Core.Localization;

namespace ZoomIN.App.Lifecycle;

/// <summary>
/// Tray notices of a tray app start: a new version (checked in the background if allowed),
/// missing UIAccess (ADR-0017) and, once after the update, the new mouse follow mode (M17 A1).
/// None may disturb the magnifier.
/// </summary>
public sealed class StartupNotices
{
    private readonly ITrayIcon _tray;
    private readonly UpdateService _updates;
    private readonly IUiAccessProbe _uiAccess;
    private readonly SettingsService _settings;
    private readonly SettingsLoader _loader;
    private readonly ILocalizer _texts;
    private readonly ILogger _log;

    public StartupNotices(
        ITrayIcon tray, UpdateService updates, IUiAccessProbe uiAccess, SettingsService settings, SettingsLoader loader,
        ILocalizer texts, ILogger<StartupNotices> log)
    {
        _tray = tray;
        _updates = updates;
        _uiAccess = uiAccess;
        _settings = settings;
        _loader = loader;
        _texts = texts;
        _log = log;
    }

    public void Start()
    {
        _updates.UpdateAvailable += update => _tray.Notify(_texts.Get("UpdateTitle"), _texts.Format("UpdateText", update.Version));
        _ = RunUpdateChecksAsync();

        _loader.ReportMigration();

        // A second notice right away replaces the first (one balloon per icon). The mouse follow
        // hint comes once, the UIAccess notice every start: on that one start the hint wins.
        var mouseFollowHint = _settings.Current.MouseFollowHintPending;

        // Once per start; the overview keeps a banner with the install button (ADR-0017).
        if (!_uiAccess.IsActive)
        {
            _log.UiAccessInactive();
            if (!mouseFollowHint)
            {
                _tray.Notify(_texts.Get("UiAccess.Title"), _texts.Get("UiAccess.Tray"));
            }
        }

        // Once after the update; a click opens the page where the user can change it back.
        if (mouseFollowHint)
        {
            _tray.Notify(_texts.Get("MouseFollowHint.Title"), _texts.Get("MouseFollowHint.Text"), TrayCommand.OpenTracking);
            _settings.Update(s => s with { MouseFollowHintPending = false });
            _log.MouseFollowHintShown();
        }
    }

    private async Task RunUpdateChecksAsync()
    {
        try
        {
            await _updates.RunChecksAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            // Never important enough to disturb the magnifier; the feed already logged it.
            _log.UpdateChecksStopped(ex.Message);
        }
    }
}
