namespace ZoomIN.Core.Lifecycle;

/// <summary>
/// Files through which a crashed ZoomIN tells the watchdog what to clean up (ADR-0007).
/// Written by Platform, read by the watchdog; both only know Core.
/// </summary>
public static class RecoveryFiles
{
    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZoomIN");

    /// <summary>Window handle (decimal) of the registered AppBar while the docked strip is reserved.</summary>
    public static string AppBarWindow { get; } = Path.Combine(Directory, "appbar.hwnd");
}
