using Microsoft.Extensions.Logging;
using ZoomIN.Core.Updates;

namespace ZoomIN.Platform.Updates;

/// <summary>
/// Folder moves of the install step (ADR-0015): version folders, the "current" link and leftovers.
/// A folder that is replaced is moved aside first and deleted only after the switch, so a failure
/// never leaves the active version half deleted (review 2026-11-17).
/// </summary>
internal sealed class InstallFolders(ILogger log)
{
    /// <summary>Puts the checked staging folder in place of <paramref name="target"/> and points "current" at it.</summary>
    public void MoveIntoPlace(InstallLayout layout, string staging, string target)
    {
        // Ends in the staging suffix: if deleting it fails now, the next update removes it.
        var aside = target + ".old" + InstallLayout.StagingSuffix;
        DeleteFolder(aside);
        var replaced = Directory.Exists(target);
        if (replaced)
        {
            Directory.Move(target, aside);
        }

        try
        {
            Directory.Move(staging, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (replaced)
            {
                Directory.Move(aside, target);
            }

            throw;
        }

        SwitchCurrent(layout, target);
        if (replaced && !TryDelete(() => DeleteFolder(aside)))
        {
            log.KeptInUse("replaced version folder");
        }
    }

    public void RemoveOld(InstallLayout layout, AppVersion active, AppVersion? previous)
    {
        var names = Directory.EnumerateDirectories(layout.Root).Select(Path.GetFileName).OfType<string>();
        foreach (var name in InstallLayout.FoldersToRemove(names, active, previous))
        {
            // Still in use (an old ZoomIN that did not exit yet): the next update removes it.
            if (!TryDelete(() => Directory.Delete(Path.Combine(layout.Root, name), recursive: true)))
            {
                log.KeptInUse(name);
            }
        }
    }

    // Removes a folder or a directory link; for a link only the link goes, never its target.
    public static void DeleteFolder(string path)
    {
        var info = new DirectoryInfo(path);
        if (info.LinkTarget is not null)
        {
            info.Delete();
        }
        else if (info.Exists)
        {
            info.Delete(recursive: true);
        }
    }

    public static bool TryDelete(Action delete)
    {
        try
        {
            delete();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    // A directory link cannot be replaced in one step; the new link is prepared next to it, so the
    // gap without "current" is a single delete + rename.
    private static void SwitchCurrent(InstallLayout layout, string target)
    {
        var next = layout.Current + ".new";
        DeleteFolder(next);
        Directory.CreateSymbolicLink(next, target);
        DeleteFolder(layout.Current);
        Directory.Move(next, layout.Current);
    }
}
