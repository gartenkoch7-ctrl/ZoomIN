namespace ZoomIN.Core.Updates;

/// <summary>
/// Folder layout of an installation (ADR-0015): every version in its own folder, a directory link
/// "current" to the active one. Shortcuts and autostart point at current\ZoomIN.exe, so an update
/// adds a folder and re-targets the link without touching files that are running.
/// <code>
/// C:\Program Files\ZoomIN\
///   app-1.0.0\ZoomIN.exe
///   app-1.1.0\ZoomIN.exe
///   current  → app-1.1.0
/// </code>
/// </summary>
public sealed record InstallLayout(string Root)
{
    public const string CurrentName = "current";
    public const string VersionPrefix = "app-";
    public const string StagingSuffix = ".tmp";
    public const string ExecutableName = "ZoomIN.exe";

    public string Current => Path.Combine(Root, CurrentName);

    public string CurrentExecutable => Path.Combine(Current, ExecutableName);

    public string VersionFolder(AppVersion version) => Path.Combine(Root, FolderName(version));

    public static string FolderName(AppVersion version) => VersionPrefix + version;

    /// <summary>Recognises an installation from the running executable's path.</summary>
    /// <returns>False for developer builds, the portable ZIP and the old flat layout: they cannot update themselves.</returns>
    public static bool TryFromExecutable(string? executablePath, out InstallLayout layout)
    {
        layout = new InstallLayout(string.Empty);
        var folder = Path.GetDirectoryName(executablePath);
        var root = Path.GetDirectoryName(folder);
        var name = Path.GetFileName(folder);
        if (root is null || name is null ||
            !(name.Equals(CurrentName, StringComparison.OrdinalIgnoreCase) || TryParseFolder(name, out _)))
        {
            return false;
        }

        layout = new InstallLayout(root);
        return true;
    }

    public static bool TryParseFolder(string folderName, out AppVersion version)
    {
        version = new AppVersion(0, 0, 0);
        return folderName.StartsWith(VersionPrefix, StringComparison.OrdinalIgnoreCase) &&
               AppVersion.TryParse(folderName[VersionPrefix.Length..], out version);
    }

    /// <summary>
    /// Folders that may be deleted after switching to <paramref name="active"/>: older versions and
    /// leftovers of aborted updates. The previous version stays for an instant way back and because
    /// it may still be running until ZoomIN restarts.
    /// </summary>
    public static IReadOnlyList<string> FoldersToRemove(IEnumerable<string> folderNames, AppVersion active, AppVersion? previous) =>
        folderNames
            .Where(name => name.EndsWith(StagingSuffix, StringComparison.OrdinalIgnoreCase) ||
                           (TryParseFolder(name, out var version) && version != active && version != previous))
            .ToList();
}
