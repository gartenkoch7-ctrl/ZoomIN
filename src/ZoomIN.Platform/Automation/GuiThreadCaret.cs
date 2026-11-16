using System.Drawing;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.Automation;

/// <summary>
/// The Win32 system caret of the foreground thread (GetGUIThreadInfo). Cheapest source and exact
/// for classic Win32/WinForms edits; applications that draw their own caret report nothing.
/// </summary>
internal static unsafe class GuiThreadCaret
{
    /// <summary>Reads the caret and the window with keyboard focus of the foreground thread.</summary>
    public static PhysicalRect? Read(out HWND focusWindow)
    {
        var info = new GUITHREADINFO { cbSize = (uint)sizeof(GUITHREADINFO) };
        if (!PInvoke.GetGUIThreadInfo(0, ref info))
        {
            focusWindow = HWND.Null;
            return null;
        }

        focusWindow = info.hwndFocus.IsNull ? info.hwndActive : info.hwndFocus;
        return info.hwndCaret.IsNull ? null : ToScreen(info.hwndCaret, info.rcCaret);
    }

    private static PhysicalRect? ToScreen(HWND window, RECT r)
    {
        // rcCaret is in the client coordinates of the caret window, i.e. in that window's DPI
        // space. Converted from a per-monitor-aware thread, a DPI-unaware or system-aware window
        // on a scaled monitor would put the caret off by the scale factor (too far right/down,
        // too small). So: client → screen in the window's own DPI context, then → physical.
        var topLeft = new Point(r.left, r.top);
        var bottomRight = new Point(Math.Max(r.right, r.left + 1), Math.Max(r.bottom, r.top + 1));
        var previous = PInvoke.SetThreadDpiAwarenessContext(PInvoke.GetWindowDpiAwarenessContext(window));
        try
        {
            if (!PInvoke.ClientToScreen(window, ref topLeft) || !PInvoke.ClientToScreen(window, ref bottomRight))
            {
                return null;
            }
        }
        finally
        {
            PInvoke.SetThreadDpiAwarenessContext(previous);
        }

        // Fails (and leaves the point unchanged) only if it lies outside the window; a caret
        // there is clipped anyway, so the unconverted point is the best guess.
        PInvoke.LogicalToPhysicalPointForPerMonitorDPI(window, ref topLeft);
        PInvoke.LogicalToPhysicalPointForPerMonitorDPI(window, ref bottomRight);

        // Some applications report a zero-width caret.
        return new PhysicalRect(topLeft.X, topLeft.Y, Math.Max(bottomRight.X - topLeft.X, 1), Math.Max(bottomRight.Y - topLeft.Y, 1));
    }
}
