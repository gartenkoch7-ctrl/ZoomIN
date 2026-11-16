using Microsoft.Extensions.Logging;
using ZoomIN.Core.Lifecycle;

namespace ZoomIN.Platform.Lifecycle;

/// <summary>
/// Deletes the autostart tasks of every account at uninstall (ADR-0030, Q9). The signed-in user's
/// own task goes with the switch; other accounts' tasks would otherwise greet each of their
/// sign-ins with a missing-file error from rundll32. Needs administrator rights: only the elevated
/// removal step may read the task store and delete other users' tasks.
/// </summary>
/// <param name="tasksFolder">The task store; one file per task, named like the task. Tests pass a temp folder.</param>
public sealed class AutostartTaskCleanup(ISchtasks schtasks, string tasksFolder, ILogger log)
{
    /// <summary>For the elevated removal: the real task store and schtasks.</summary>
    public AutostartTaskCleanup(ILogger log)
        : this(new SchtasksProcess(), Path.Combine(Environment.SystemDirectory, "Tasks"), log)
    {
    }

    /// <returns>How many tasks were deleted.</returns>
    public int RemoveAll()
    {
        string[] files;
        try
        {
            // Only the root folder: that is where AutostartTask registers its tasks.
            files = Directory.GetFiles(tasksFolder, AutostartTask.NamePrefix + "*", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.TaskCommandFailed("list", ex.HResult);
            return 0;
        }

        var removed = 0;
        foreach (var file in files)
        {
            var exitCode = schtasks.Run(["/Delete", "/TN", Path.GetFileName(file), "/F"]);
            if (exitCode == 0)
            {
                removed++;
            }
            else
            {
                log.TaskCommandFailed("delete", exitCode);
            }
        }

        log.AutostartTasksRemoved(removed);
        return removed;
    }
}
