namespace ZoomIN.Core.SecureDesktop;

/// <summary>
/// Rules of the elevated step "ZoomIN.exe --copy-secure-logs &lt;folder&gt;" (M10, Q7): it copies the logs
/// of the secure instance from SYSTEM's profile into the diagnostics folder of the user who asked.
/// It runs with administrator rights and writes into a folder that user controls, so the target is
/// held to one exact shape: a new folder "secure-logs-&lt;32 hex digits&gt;" directly inside that user's
/// diagnostics folder, a plain "X:\…" path without ".", "..", links or streams. Only *.log and *.jsonl
/// files are copied, newest first, at most <see cref="MaxBytes"/> together.
/// </summary>
public static class SecureLogCopyRule
{
    /// <summary>The command-line switch of the elevated step.</summary>
    public const string Switch = "--copy-secure-logs";

    /// <summary>Upper limit for all copied files together.</summary>
    public const long MaxBytes = 20L * 1024 * 1024;

    /// <summary>Name prefix of the target folder; the rest is a GUID in "N" format.</summary>
    public const string FolderPrefix = "secure-logs-";

    private const int GuidLength = 32;

    /// <summary>"--copy-secure-logs &lt;folder&gt;" and nothing else.</summary>
    public static bool TryParse(IReadOnlyList<string> args, out string target)
    {
        ArgumentNullException.ThrowIfNull(args);
        target = args is [Switch, var folder] ? folder : string.Empty;
        return target.Length > 0;
    }

    /// <summary>A fresh target folder name for <see cref="IsAllowedTarget"/>.</summary>
    public static string NewFolderName() => FolderPrefix + Guid.NewGuid().ToString("N");

    /// <summary>
    /// The target is "&lt;diagnostics&gt;\secure-logs-&lt;32 hex&gt;" exactly. String rules only; the
    /// elevated step also checks the drive, links and the path Windows really opens.
    /// </summary>
    /// <param name="target">Folder from the command line.</param>
    /// <param name="diagnostics">The diagnostics folder of the account running the step.</param>
    public static bool IsAllowedTarget(string? target, string? diagnostics)
    {
        if (!LocalPathRule.IsDriveLetterPath(target) || !LocalPathRule.IsDriveLetterPath(diagnostics) || HasDotSegment(diagnostics!))
        {
            return false;
        }

        // HasDotSegment refused a trailing backslash (an empty segment), so this is the folder plus one.
        var root = diagnostics + "\\";
        if (!target!.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var name = target[root.Length..];
        return name.Length == FolderPrefix.Length + GuidLength
            && name.StartsWith(FolderPrefix, StringComparison.Ordinal)
            && name[FolderPrefix.Length..].All(char.IsAsciiHexDigitLower);
    }

    /// <summary>Only the log and operations files of the secure instance.</summary>
    public static bool IsCopiedFile(string fileName) =>
        fileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase);

    /// <summary>The files to copy: newest first, as long as they fit into <paramref name="budget"/> together.</summary>
    /// <param name="files">Candidates with size and last write time.</param>
    public static IReadOnlyList<T> Select<T>(IEnumerable<T> files, Func<T, long> length, Func<T, DateTimeOffset> written, long budget = MaxBytes)
    {
        var chosen = new List<T>();
        var left = budget;
        foreach (var file in files.OrderByDescending(written))
        {
            var size = length(file);
            if (size <= left)
            {
                chosen.Add(file);
                left -= size;
            }
        }

        return chosen;
    }

    // "." and ".." segments, and names Windows would trim ("logs." = "logs"), hide where a path leads.
    private static bool HasDotSegment(string path) =>
        path.Split('\\').Skip(1).Any(segment => segment.Length == 0 || segment.EndsWith('.') || segment.EndsWith(' '));
}
