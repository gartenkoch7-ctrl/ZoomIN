using System.Drawing;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ZoomIN.Platform.WindowDrag;

/// <summary>Reads around a window's move/size loop (G1): is it still running, which monitor has the pointer.</summary>
internal static unsafe class MoveSizeLoop
{
    /// <summary>True while the window's thread is in its move/size loop for this window.</summary>
    public static bool IsRunning(uint thread, nint window)
    {
        var info = new GUITHREADINFO { cbSize = (uint)sizeof(GUITHREADINFO) };
        return PInvoke.GetGUIThreadInfo(thread, ref info)
            && (info.flags & GUITHREADINFO_FLAGS.GUI_INMOVESIZE) != 0
            && info.hwndMoveSize == (HWND)window;
    }

    /// <summary>
    /// The monitor under the pointer as a handle, only to notice a change cheaply on every move;
    /// the App maps the pointer to the monitor id itself. Null handle if the pointer is unknown.
    /// </summary>
    public static HMONITOR MonitorUnderPointer() =>
        PInvoke.GetCursorPos(out var cursor)
            ? PInvoke.MonitorFromPoint(new Point(cursor.X, cursor.Y), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST)
            : HMONITOR.Null;
}
