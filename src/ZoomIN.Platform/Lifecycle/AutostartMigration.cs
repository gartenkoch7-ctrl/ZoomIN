using System.Diagnostics;
using System.Security;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;

namespace ZoomIN.Platform.Lifecycle;

/// <summary>
/// Moves the autostart of an installed ZoomIN once to the scheduled task (ADR-0030, Q6 and Q9): a
/// "ZoomIN" Run value or an entry in the Ease of Access start list (spike A1 builds) becomes the
/// task. Without either the autostart stays as it is. Only logged, no hint. The portable ZIP and
/// developer builds keep the Run key (Q10). Runs at every start of the tray app; after the move it
/// finds nothing.
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
            var from = autostart.HasRunValue ? "run" : autostart.HasListEntry ? "ease_of_access" : "none";
            activity?.SetTag("zoomin.autostart.from", from);
            if (from == "none")
            {
                return false;
            }

            // Creates the task, then removes the old entries only if that worked. A failure retries
            // at the next start (one schtasks call; blocked task creation is rare).
            if (!autostart.TrySetEnabled(true))
            {
                activity?.SetStatus(ActivityStatusCode.Error, "task missing");
                log.MigrationFailed("task missing");
                return false;
            }

            log.AutostartMigrated(from);
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
