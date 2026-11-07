using Microsoft.Extensions.Logging;
using ZoomIN.App.Localization;
using ZoomIN.App.Updates;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Lifecycle;

namespace ZoomIN.App.Lifecycle;

/// <summary>
/// Tray notices of a tray app start: a new version (checked in the background if allowed) and
/// missing UIAccess (ADR-0017). Neither may disturb the magnifier.
/// </summary>
public sealed class StartupNotices
{
    private readonly ITrayIcon _tray;
    private readonly UpdateService _updates;
    private readonly IUiAccessProbe _uiAccess;
    private readonly ILogger _log;

    public StartupNotices(ITrayIcon tray, UpdateService updates, IUiAccessProbe uiAccess, ILogger<StartupNotices> log)
    {
        _tray = tray;
        _updates = updates;
        _uiAccess = uiAccess;
        _log = log;
    }

    public void Start()
    {
        _updates.UpdateAvailable += update => _tray.Notify(Text.UpdateTitle, Text.Format("UpdateText", update.Version));
        _ = RunUpdateChecksAsync();

        // Once per start; the overview keeps a banner with the install button (ADR-0017).
        if (!_uiAccess.IsActive)
        {
            _log.UiAccessInactive();
            _tray.Notify(Text.Get("UiAccess.Title"), Text.Get("UiAccess.Tray"));
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
