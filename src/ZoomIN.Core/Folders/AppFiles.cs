namespace ZoomIN.Core.Folders;

/// <summary>
/// The files and folders ZoomIN keeps in its <see cref="IAppFolders"/>, as pure path building
/// (no IO): every name in one place, so App, Platform, watchdog and setup agree.
/// </summary>
public static class AppFiles
{
    /// <summary>Hourly log and trace files (ADR-0019).</summary>
    public static string Logs(this IAppFolders folders) => Path.Combine(Local(folders), "logs");

    /// <summary>Diagnostics packages the user creates on the diagnostics page.</summary>
    public static string Diagnostics(this IAppFolders folders) => Path.Combine(Local(folders), "diagnostics");

    /// <summary>Debug snapshots (M13).</summary>
    public static string Debug(this IAppFolders folders) => Path.Combine(Local(folders), "debug");

    /// <summary>Downloaded update packages (M9).</summary>
    public static string Updates(this IAppFolders folders) => Path.Combine(Local(folders), "updates");

    /// <summary>Downloaded setup programs (version choice, M9).</summary>
    public static string Setup(this IAppFolders folders) => Path.Combine(Local(folders), "setup");

    /// <summary>Crash times the watchdog keeps for its restart policy (ADR-0007).</summary>
    public static string CrashLog(this IAppFolders folders) => Path.Combine(Local(folders), "crashes.log");

    /// <summary>The watchdog gave up on the secure-desktop instance (ADR-0016), with the time.</summary>
    public static string SecureGaveUp(this IAppFolders folders) => Path.Combine(Local(folders), "secure-gave-up.txt");

    /// <summary>
    /// Window handle (decimal) of the registered AppBar while the docked strip is reserved: a
    /// crashed ZoomIN tells the watchdog what to give back (ADR-0007).
    /// </summary>
    public static string AppBarWindow(this IAppFolders folders) => Path.Combine(Local(folders), "appbar.hwnd");

    /// <summary>The user's settings (ADR-0004).</summary>
    public static string Settings(this IAppFolders folders) => Path.Combine(Roaming(folders), "settings.json");

    /// <summary>The user's profiles, one file each.</summary>
    public static string Profiles(this IAppFolders folders) => Path.Combine(Roaming(folders), "profiles");

    private static string Local(IAppFolders folders)
    {
        ArgumentNullException.ThrowIfNull(folders);
        return folders.Local;
    }

    private static string Roaming(IAppFolders folders)
    {
        ArgumentNullException.ThrowIfNull(folders);
        return folders.Roaming;
    }
}
