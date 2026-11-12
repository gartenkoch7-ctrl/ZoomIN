using System.Diagnostics;
using System.Security;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;

namespace ZoomIN.Platform.Lifecycle;

/// <summary>
/// Moves the autostart of an installed ZoomIN once from the Run key to the Ease of Access list
/// (ADR-0030, decision Q6): a "ZoomIN" Run value is removed and the list entry added. Without a Run
/// value the autostart stays as it is. Only logged, no hint. The portable ZIP and developer builds
/// keep the Run key (Q10). Runs at every start of the tray app; after the move it finds nothing.
/// </summary>
public sealed class AutostartMigration(AutostartSelector autostart, ILogger log)
{
    /// <returns>True if the autostart was moved now.</returns>
    public bool Migrate()
    {
        if (!autostart.IsInstalled)
        {
            return false;
        }

        using var activity = Telemetry.Source.StartActivity("zoomin.autostart.migrate");
        try
        {
            var fromRun = autostart.HasRunValue;
            activity?.SetTag("zoomin.autostart.from", fromRun ? "run" : "none");
            if (!fromRun)
            {
                return false;
            }

            // An update is applied by the previous version, which may not write the AT entry yet;
            // the list would then start nothing, so the Run value stays until it exists.
            if (!autostart.UsesEaseOfAccess)
            {
                activity?.SetTag("zoomin.autostart.postponed", true);
                log.MigrationPostponed();
                return false;
            }

            // Adds the list entry, then removes the Run value.
            autostart.SetEnabled(true);
            log.AutostartMigrated();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            log.MigrationFailed(ex.GetType().Name);
            return false;
        }
    }
}
