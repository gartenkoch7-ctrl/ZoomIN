using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.System.StationsAndDesktops;
using Windows.Win32.System.Threading;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.Zoom;
using ZoomIN.Platform.WindowFilters;

namespace ZoomIN.Platform.SecureDesktop;

/// <summary>
/// <see cref="ISecureDesktopProbe"/> over Win32 (M10 checks). Called once after the first image,
/// never per frame. Reads window classes and rectangles only, never titles.
/// </summary>
public sealed unsafe class Win32SecureDesktopProbe : ISecureDesktopProbe
{
    // A desktop has a few hundred top-level windows; the bound only guards against runaway lists.
    private const int MaxWindows = 10_000;

    // One delegate for the life of the process, so EnumWindows never sees a collected callback.
    private static readonly WNDENUMPROC s_collect = Collect;

    [ThreadStatic]
    private static List<HWND>? t_windows;

    public string? DesktopName()
    {
        var desktop = PInvoke.GetThreadDesktop(PInvoke.GetCurrentThreadId());
        if (desktop.IsNull)
        {
            return null;
        }

        // Desktop names are short ("Winlogon", "Default"); 256 characters is plenty.
        var name = stackalloc char[256];
        uint needed;
        if (!PInvoke.GetUserObjectInformation((HANDLE)desktop.Value, USER_OBJECT_INFORMATION_INDEX.UOI_NAME, name, 256 * sizeof(char), &needed))
        {
            return null;
        }

        return new string(name);
    }

    public IReadOnlyList<ProbedWindow> OwnOverlays()
    {
        var own = (uint)Environment.ProcessId;
        var overlays = new List<ProbedWindow>();
        foreach (var window in VisibleWindows())
        {
            uint process;
            var thread = PInvoke.GetWindowThreadProcessId(window, &process);
            if (thread != 0 && process == own && ClassOf(window) == OverlayWindowClass.Name && Bounds(window) is { } bounds)
            {
                overlays.Add(new ProbedWindow((nint)window.Value, OverlayWindowClass.Name, bounds));
            }
        }

        return overlays;
    }

    public ProbedWindow? TopWindowAt(PhysicalPoint point)
    {
        foreach (var window in VisibleWindows())
        {
            if (Bounds(window) is { } bounds && bounds.Contains(point))
            {
                return new ProbedWindow((nint)window.Value, ClassOf(window), bounds);
            }
        }

        return null;
    }

    // The visible frame, without the invisible resize border a window next to the centre would reach it with.
    private static PhysicalRect? Bounds(HWND window) =>
        WindowEnumerator.TryGetBounds(window, out var bounds) ? bounds : null;

    public uint? DisplayAffinity(nint window) =>
        PInvoke.GetWindowDisplayAffinity((HWND)window, out var affinity) ? affinity : null;

    public bool? IsInJob() => InJob(PInvoke.GetCurrentProcess());

    public ProbedProcess Process(int processId)
    {
        var handle = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_ACCESS_RIGHTS.PROCESS_SYNCHRONIZE, false, (uint)processId);
        if (handle.IsNull)
        {
            // Gone (or never ours): either way no watchdog is looking after this instance.
            return new ProbedProcess(false, null);
        }

        try
        {
            // The id may meanwhile belong to another program: only our own exe counts as the watchdog.
            var alive = PInvoke.WaitForSingleObject(handle, 0) == WAIT_EVENT.WAIT_TIMEOUT && IsOwnExe(handle);
            return new ProbedProcess(alive, InJob(handle));
        }
        finally
        {
            PInvoke.CloseHandle(handle);
        }
    }

    // By file name: the launch path may be spelled differently (8.3) from the one Windows reports.
    private static bool IsOwnExe(HANDLE process)
    {
        var buffer = stackalloc char[1024];
        var size = 1024u;
        return PInvoke.QueryFullProcessImageName(process, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, buffer, &size)
            && string.Equals(Path.GetFileName(new string(buffer, 0, (int)size)), Path.GetFileName(Environment.ProcessPath), StringComparison.OrdinalIgnoreCase);
    }

    private static bool? InJob(HANDLE process)
    {
        BOOL inJob;
        return PInvoke.IsProcessInJob(process, HANDLE.Null, &inJob) ? inJob : null;
    }

    /// <summary>Top-level windows of this desktop from the top of the z-order, visible and not cloaked.</summary>
    /// <remarks>
    /// EnumWindows takes the list in one go: walking GW_HWNDNEXT would stop early when a window is
    /// destroyed during the walk (LogonUI and consent windows come and go), and miss the overlay.
    /// </remarks>
    private static List<HWND> VisibleWindows()
    {
        var all = new List<HWND>();
        t_windows = all;
        try
        {
            PInvoke.EnumWindows(s_collect, default);
        }
        finally
        {
            t_windows = null;
        }

        return all.FindAll(window => PInvoke.IsWindowVisible(window) && !IsCloaked(window));
    }

    private static BOOL Collect(HWND window, LPARAM data)
    {
        var all = t_windows!;
        all.Add(window);
        return all.Count < MaxWindows;
    }

    private static bool IsCloaked(HWND window)
    {
        uint cloaked;
        return PInvoke.DwmGetWindowAttribute(window, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(uint)).Succeeded && cloaked != 0;
    }

    private static string ClassOf(HWND window)
    {
        // Window class names are at most 256 characters.
        var name = stackalloc char[257];
        var length = PInvoke.GetClassName(window, name, 257);
        return length > 0 ? new string(name, 0, length) : string.Empty;
    }
}
