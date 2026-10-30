using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ZoomIN.Watchdog.Notification;

/// <summary>
/// Shows a Windows notification through a temporary tray icon balloon. Windows 10/11 render it
/// as a toast; unlike the AppNotification API it needs no package identity.
/// </summary>
internal static unsafe class TrayNotification
{
    // MAKEINTRESOURCE(IDI_WARNING)
    private const int WarningIconId = 32515;

    public static void Show(string title, string message, TimeSpan duration)
    {
        // Any window can own a tray icon; a hidden STATIC control needs no window class.
        var window = PInvoke.CreateWindowEx(0, "STATIC", "ZoomIN Watchdog", 0, 0, 0, 0, 0, HWND.Null, null, null, null);
        try
        {
            var data = new NOTIFYICONDATAW
            {
                cbSize = (uint)sizeof(NOTIFYICONDATAW),
                hWnd = window,
                uID = 1,
                uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP | NOTIFY_ICON_DATA_FLAGS.NIF_INFO,
                hIcon = PInvoke.LoadIcon(HINSTANCE.Null, (PCWSTR)(char*)WarningIconId),
                dwInfoFlags = NOTIFY_ICON_INFOTIP_FLAGS.NIIF_WARNING,
                szTip = "ZoomIN",
                szInfoTitle = title,
                szInfo = message,
            };

            PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, data);
            Thread.Sleep(duration);
            PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, data);
        }
        finally
        {
            PInvoke.DestroyWindow(window);
        }
    }
}
