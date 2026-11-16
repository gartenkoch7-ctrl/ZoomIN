namespace ZoomIN.Core.WindowFilters;

/// <summary>
/// Choices from the start (ADR-0020): photo, video and image editing apps keep their colours,
/// the Windows interface is never filtered automatically. User rules win over these.
/// </summary>
public static class WindowFilterPresets
{
    // Program file names. Own selection of well-known apps.
    private static readonly HashSet<string> s_media = new(StringComparer.OrdinalIgnoreCase)
    {
        "vlc.exe", "mpv.exe", "mpc-hc.exe", "mpc-hc64.exe", "mpc-be.exe", "mpc-be64.exe", "wmplayer.exe",
        "potplayer.exe", "potplayer64.exe", "potplayermini.exe", "potplayermini64.exe", "kodi.exe",
        "microsoft.media.player.exe", "video.ui.exe", "microsoft.photos.exe", "photosapp.exe",
        "photoshop.exe", "lightroom.exe", "lightroomclassic.exe", "illustrator.exe", "afterfx.exe",
        "adobe premiere pro.exe", "resolve.exe", "krita.exe", "inkscape.exe", "paintdotnet.exe",
        "mspaint.exe", "affinityphoto.exe", "affinityphoto2.exe", "affinitydesigner.exe",
        "affinitydesigner2.exe", "darktable.exe", "rawtherapee.exe", "i_view32.exe", "i_view64.exe",
        "xnview.exe", "xnviewmp.exe", "clipstudiopaint.exe", "blender.exe", "obs64.exe",
    };

    // Shell processes whose every window belongs to the Windows interface.
    private static readonly HashSet<string> s_shellProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "shellexperiencehost.exe", "startmenuexperiencehost.exe", "searchhost.exe", "searchapp.exe",
        "searchui.exe", "lockapp.exe", "textinputhost.exe", "shellhost.exe",
    };

    // Explorer windows that are the taskbar, desktop, task view and friends (file windows are apps).
    private static readonly HashSet<string> s_shellClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman", "WorkerW", "NotifyIconOverflowWindow",
        "TaskListThumbnailWnd", "MultitaskingViewFrame", "XamlExplorerHostIslandWindow", "ForegroundStaging",
        "TopLevelWindowForOverflowXamlIsland", "Shell_InputSwitchTopLevelWindow",
    };

    /// <summary>Photo, video or image app: starts with "None".</summary>
    public static bool IsMedia(string process) =>
        s_media.Contains(process) || process.StartsWith("gimp", StringComparison.OrdinalIgnoreCase);

    /// <summary>Part of the Windows interface.</summary>
    public static bool IsShell(WindowKind kind) =>
        s_shellProcesses.Contains(kind.Process)
        || (string.Equals(kind.Process, "explorer.exe", StringComparison.OrdinalIgnoreCase) && s_shellClasses.Contains(kind.WindowClass));

    /// <summary>The choice that applies without a user rule.</summary>
    public static WindowFilterChoice ChoiceWithoutRule(WindowKind kind) =>
        IsMedia(kind.Process) ? WindowFilterChoice.None : WindowFilterChoice.Automatic;
}
