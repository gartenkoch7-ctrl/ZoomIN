using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.Sharing;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.Sharing;

/// <summary>
/// Reads window rectangles and styles only (ADR-0018): no hooks, no injection, no reading of
/// window contents, so the meeting app is observed the way any window manager sees it.
/// </summary>
internal static unsafe class MeetingWindowReader
{
    // New Teams (WebView2 host) and classic Teams. The frame belongs to the host process.
    private static readonly string[] s_processNames = ["ms-teams", "Teams"];

    // Kept in a static field so the GC never collects the delegate Windows calls back into.
    private static readonly WNDENUMPROC s_collect = Collect;

    public static HashSet<uint> MeetingProcessIds()
    {
        var ids = new HashSet<uint>();
        foreach (var name in s_processNames)
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                ids.Add((uint)process.Id);
                process.Dispose();
            }
        }

        return ids;
    }

    /// <summary>Visible, uncloaked top-level windows of the given processes.</summary>
    public static List<MeetingWindow> WindowsOf(HashSet<uint> processIds)
    {
        var context = (processIds, new List<MeetingWindow>());
        var handle = GCHandle.Alloc(context);
        try
        {
            PInvoke.EnumWindows(s_collect, (LPARAM)GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }

        return context.Item2;
    }

    /// <summary>Visible bounds of a window; null if it is gone, minimised or hidden.</summary>
    public static PhysicalRect? BoundsOf(HWND window)
    {
        if (!PInvoke.IsWindow(window) || PInvoke.IsIconic(window) || !PInvoke.IsWindowVisible(window))
        {
            return null;
        }

        // The extended frame excludes the invisible resize border Windows 10/11 adds around windows.
        RECT r;
        if (PInvoke.DwmGetWindowAttribute(window, DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, &r, (uint)sizeof(RECT)).Failed
            && !PInvoke.GetWindowRect(window, out r))
        {
            return null;
        }

        return r.right > r.left && r.bottom > r.top ? new PhysicalRect(r.left, r.top, r.right - r.left, r.bottom - r.top) : null;
    }

    public static bool IsCloaked(HWND window)
    {
        int cloaked;
        return PInvoke.DwmGetWindowAttribute(window, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(int)).Succeeded
            && cloaked != 0;
    }

    private static BOOL Collect(HWND window, LPARAM data)
    {
        var (processIds, windows) = ((HashSet<uint>, List<MeetingWindow>))GCHandle.FromIntPtr(data).Target!;
        uint processId;
        _ = PInvoke.GetWindowThreadProcessId(window, &processId);
        if (!processIds.Contains(processId) || !PInvoke.IsWindowVisible(window) || IsCloaked(window))
        {
            return true;
        }

        RECT r;
        if (!PInvoke.GetWindowRect(window, out r) || r.right <= r.left || r.bottom <= r.top)
        {
            return true;
        }

        var exStyle = (WINDOW_EX_STYLE)(uint)PInvoke.GetWindowLongPtr(window, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        var clickThrough = exStyle.HasFlag(WINDOW_EX_STYLE.WS_EX_LAYERED) && exStyle.HasFlag(WINDOW_EX_STYLE.WS_EX_TRANSPARENT);
        windows.Add(new MeetingWindow(
            (long)(nint)window.Value,
            new PhysicalRect(r.left, r.top, r.right - r.left, r.bottom - r.top),
            exStyle.HasFlag(WINDOW_EX_STYLE.WS_EX_TOPMOST),
            clickThrough));
        return true;
    }
}
