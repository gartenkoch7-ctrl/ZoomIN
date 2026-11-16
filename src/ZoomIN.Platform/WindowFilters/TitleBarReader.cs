using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.WindowFilters;

/// <summary>
/// Height of a window's title bar strip, so it is measured and filtered apart from the page. Many
/// Windows apps (Settings, Explorer, store apps) draw their title bar inside the client area, so
/// the client rectangle alone does not find it; the caption buttons drawn by DWM do.
/// </summary>
internal static unsafe class TitleBarReader
{
    // A title bar is never more than a third of the window; more means the reading is off
    // (e.g. a tiny window), and the whole window is measured as before.
    private const int MaxShareDivisor = 3;

    // STATE_SYSTEM_INVISIBLE in TITLEBARINFO.rgstate[0]: the window has no title bar.
    private const uint StateSystemInvisible = 0x8000;

    /// <summary>Title bar height in physical pixels from the top of <paramref name="bounds"/>; 0 if none was found.</summary>
    public static int HeightOf(HWND hwnd, PhysicalRect bounds)
    {
        var bottom = CaptionButtonsBottom(hwnd) ?? SystemTitleBarBottom(hwnd);
        if (bottom is not { } y)
        {
            return 0;
        }

        var height = (int)(y - bounds.Y);
        return height > 0 && height * MaxShareDivisor <= bounds.Height ? height : 0;
    }

    // DWMWA_CAPTION_BUTTON_BOUNDS is relative to the window rectangle (with its invisible border).
    private static double? CaptionButtonsBottom(HWND hwnd)
    {
        RECT buttons;
        if (PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CAPTION_BUTTON_BOUNDS, &buttons, (uint)sizeof(RECT)).Failed
            || buttons.bottom <= buttons.top || !PInvoke.GetWindowRect(hwnd, out var window))
        {
            return null;
        }

        return window.top + buttons.bottom;
    }

    // Classic windows: the system title bar itself. Not the top of the client area – a menu bar
    // lies in between and would be counted (and left unfiltered) as part of the title bar.
    private static double? SystemTitleBarBottom(HWND hwnd)
    {
        var info = new TITLEBARINFO { cbSize = (uint)sizeof(TITLEBARINFO) };
        if (!PInvoke.GetTitleBarInfo(hwnd, ref info) || (info.rgstate[0] & StateSystemInvisible) != 0
            || info.rcTitleBar.bottom <= info.rcTitleBar.top)
        {
            return null;
        }

        return info.rcTitleBar.bottom;
    }
}
