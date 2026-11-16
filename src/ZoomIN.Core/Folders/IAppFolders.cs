namespace ZoomIN.Core.Folders;

/// <summary>
/// Where ZoomIN keeps its files on this PC (ADR-0025): one service instead of every class asking
/// Windows for %AppData%. The file names inside are in <see cref="AppFiles"/>.
/// </summary>
public interface IAppFolders
{
    /// <summary>
    /// %LocalAppData%\ZoomIN of the running account: logs, recovery files, crashes, diagnostics,
    /// downloads. On the secure desktop that is SYSTEM's own profile (ADR-0016).
    /// </summary>
    string Local { get; }

    /// <summary>%AppData%\ZoomIN of the running account: settings, profiles and their backups.</summary>
    string Roaming { get; }
}
