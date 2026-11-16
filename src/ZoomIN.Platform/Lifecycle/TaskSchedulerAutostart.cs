using System.Text;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Lifecycle;

namespace ZoomIN.Platform.Lifecycle;

/// <summary>
/// Autostart of an installed ZoomIN through a scheduled task at the user's sign-in (ADR-0030, Q9).
/// Reads the task at every <see cref="IsEnabled"/>: the user may delete it in the Task Scheduler.
/// Failures are logged, not thrown; <see cref="IsEnabled"/> then tells the truth.
/// </summary>
/// <param name="executable">The stable "current" path of the installed exe.</param>
/// <param name="userSid">The signed-in user; tests pass a made-up SID.</param>
public sealed class TaskSchedulerAutostart(string executable, string userSid, ISchtasks schtasks, ILogger log) : IAutostart
{
    // schtasks /Query: 0 = the task exists, 1 = it does not; anything else (-1 timeout) is unknown.
    private const int TaskMissing = 1;

    public string TaskName { get; } = AutostartTask.Name(userSid);

    public bool IsEnabled => schtasks.Run(["/Query", "/TN", TaskName]) == 0;

    /// <summary>True if the task surely does not exist; false if it does or schtasks cannot tell.</summary>
    public bool IsSurelyMissing => schtasks.Run(["/Query", "/TN", TaskName]) == TaskMissing;

    public void SetEnabled(bool enabled) => TrySetEnabled(enabled);

    /// <returns>True if schtasks confirmed the change (no second query: a slow one could fail alone).</returns>
    public bool TrySetEnabled(bool enabled)
    {
        if (enabled)
        {
            return Create();
        }

        // Delete first: a failed query must not pass for "no task", or a task of a removed ZoomIN
        // would stay and greet every sign-in with a missing-file error.
        var exitCode = schtasks.Run(["/Delete", "/TN", TaskName, "/F"]);
        return exitCode == 0 || schtasks.Run(["/Query", "/TN", TaskName]) == TaskMissing || Check("delete", exitCode);
    }

    private bool Create()
    {
        // schtasks reads the task only from a file; UTF-16 as the XML declaration says. The temp
        // folder holds it for the length of one call only, so it needs no IAppFolders entry.
        var file = Path.Combine(Path.GetTempPath(), $"ZoomIN-autostart-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(file, AutostartTask.Xml(userSid, executable), Encoding.Unicode);
            var exitCode = schtasks.Run(["/Create", "/TN", TaskName, "/XML", file, "/F"]);

            // A timeout kills schtasks, perhaps after it registered the task: look once more.
            return Check("create", exitCode) || (exitCode == SchtasksProcess.TimedOut && IsEnabled);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.TaskCommandFailed("create", ex.HResult);
            return false;
        }
        finally
        {
            Remove(file);
        }
    }

    // A leftover temp file is harmless; failing the switch over it is not.
    private static void Remove(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private bool Check(string action, int exitCode)
    {
        if (exitCode != 0)
        {
            log.TaskCommandFailed(action, exitCode);
        }

        return exitCode == 0;
    }
}
