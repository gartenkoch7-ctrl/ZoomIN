using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.WindowFilters;

/// <summary>A visible top-level window with what can be read cheaply on every change.</summary>
internal readonly record struct RawWindow(nint Handle, PhysicalRect Bounds, uint ProcessId, nint Owner, bool PopupStyle, bool ToolWindow, bool HasCaption = false);

/// <summary>
/// Lists the visible top-level windows front to back (EnumWindows goes in Z order). Only cheap
/// calls per window: names and classes are read once per window by the tracker.
/// </summary>
internal static unsafe class WindowEnumerator
{
    // One delegate for the life of the process, so EnumWindows never sees a collected callback.
    private static readonly WNDENUMPROC s_callback = Visit;

    [ThreadStatic]
    private static List<RawWindow>? t_target;

    /// <summary>Fills <paramref name="target"/> (cleared first) with the visible windows, front to back.</summary>
    public static void Collect(List<RawWindow> target)
    {
        target.Clear();
        t_target = target;
        try
        {
            PInvoke.EnumWindows(s_callback, default);
        }
        finally
        {
            t_target = null;
        }
    }

    /// <summary>
    /// The frame as the user sees it. GetWindowRect includes the invisible resize border of
    /// Windows 10/11 (about 7 px each side), which would filter a strip of the window below.
    /// </summary>
    public static bool TryGetBounds(HWND hwnd, out PhysicalRect bounds)
    {
        RECT rect;
        if (PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, &rect, (uint)sizeof(RECT)).Failed
            && !PInvoke.GetWindowRect(hwnd, out rect))
        {
            bounds = default;
            return false;
        }

        bounds = new PhysicalRect(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
        return bounds.Width > 0 && bounds.Height > 0;
    }

    private static BOOL Visit(HWND hwnd, LPARAM lParam)
    {
        if (t_target is { } target && TryRead(hwnd, out var window))
        {
            target.Add(window);
        }

        return true;
    }

    private static bool TryRead(HWND hwnd, out RawWindow window)
    {
        window = default;
        if (!PInvoke.IsWindowVisible(hwnd) || PInvoke.IsIconic(hwnd) || IsCloaked(hwnd))
        {
            return false;
        }

        var style = (WINDOW_STYLE)(uint)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        var exStyle = (WINDOW_EX_STYLE)(uint)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

        // Click-through layered windows are overlays (ZoomIN's own, screen recorders, effects):
        // they show what lies below, so they must not hide it from the filter.
        if (exStyle.HasFlag(WINDOW_EX_STYLE.WS_EX_TRANSPARENT) && exStyle.HasFlag(WINDOW_EX_STYLE.WS_EX_LAYERED))
        {
            return false;
        }

        if (!TryGetBounds(hwnd, out var bounds))
        {
            return false;
        }

        uint processId;
        _ = PInvoke.GetWindowThreadProcessId(hwnd, &processId);
        var owner = PInvoke.GetWindow(hwnd, GET_WINDOW_CMD.GW_OWNER);
        var caption = (style & WINDOW_STYLE.WS_CAPTION) == WINDOW_STYLE.WS_CAPTION;
        var popup = style.HasFlag(WINDOW_STYLE.WS_POPUP) && !caption;
        window = new RawWindow(hwnd, bounds, processId, owner, popup, exStyle.HasFlag(WINDOW_EX_STYLE.WS_EX_TOOLWINDOW), caption);
        return true;
    }

    // Cloaked: on another virtual desktop, a suspended store app, or hidden by DWM.
    private static bool IsCloaked(HWND hwnd)
    {
        uint cloaked;
        return PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(uint)).Succeeded && cloaked != 0;
    }
}
