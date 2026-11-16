namespace ZoomIN.Core.SecureDesktop;

/// <summary>
/// Which user folders the secure-desktop instance (SYSTEM) may touch at all (ADR-0016). A user can
/// redirect their AppData to a network share; if SYSTEM opened it, Windows would sign in there with
/// the computer account. Only "X:\…" paths pass; UNC, device ("\\?\", "\\.\") and relative paths
/// do not. Platform additionally checks that the drive is a fixed local disk.
/// </summary>
public static class LocalPathRule
{
    public static bool IsDriveLetterPath(string? path) =>
        path is { Length: >= 3 }
        && char.IsAsciiLetter(path[0])
        && path[1] == ':'
        && path[2] == '\\'
        && !path.Contains('/', StringComparison.Ordinal)
        && !path.Contains(@"\\", StringComparison.Ordinal)
        && path.IndexOf(':', 2) < 0;
}
