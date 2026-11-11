using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Lifecycle;

namespace ZoomIN.Platform.Shell;

/// <summary>
/// Tray icon via Shell_NotifyIcon on its own thread with a message window (M7). Re-adds itself
/// when Explorer restarts ("TaskbarCreated"). Menu commands and notice clicks are raised on this thread.
/// </summary>
public sealed unsafe class Win32TrayIcon : ITrayIcon
{
    private const string ClassName = "ZoomIN.Tray";
    private const uint IconId = 1;

    // NIN_BALLOONUSERCLICK (WM_USER + 5): the user clicked a notice (a toast on Windows 10/11).
    private const uint BalloonUserClick = 0x0405;
    private static readonly uint s_callbackMessage = PInvoke.WM_APP + 2;
    private static readonly uint s_noticeMessage = PInvoke.WM_APP + 3;
    private static readonly uint s_taskbarCreated = PInvoke.RegisterWindowMessage("TaskbarCreated");
    private static readonly WNDPROC s_wndProc = WndProc;
    private static Win32TrayIcon? s_instance;

    private readonly ILogger _log;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private readonly TrayNotices _notices = new();
    private TrayMenu _menu = new("ZoomIN", []);
    private HWND _window;
    private nint _icon;
    private bool _added;

    public Win32TrayIcon(ILogger<Win32TrayIcon> log)
    {
        _log = log;
        s_instance = this;
        _thread = new Thread(Run) { IsBackground = true, Name = "ZoomIN Tray" };
        _thread.Start();
        _ready.Wait();
    }

    public event Action<TrayCommand>? Command;

    public void Show(TrayMenu menu)
    {
        Volatile.Write(ref _menu, menu);
        PInvoke.PostMessage(_window, PInvoke.WM_APP, default, default);
    }

    public void Notify(string title, string message, TrayCommand? clicked = null)
    {
        // Shown on the tray thread, after a Show posted before it: right after start the icon may
        // not be added yet, and NIM_MODIFY would fail without a word.
        // Cut to the fixed NOTIFYICONDATA buffers (64/256 chars with the terminator) here: copying
        // a longer text throws, and inside the window procedure that would end the process.
        _notices.Add(new TrayNotice(Fit(title, 63), Fit(message, 255), clicked));
        PInvoke.PostMessage(_window, s_noticeMessage, default, default);
    }

    // Never ends between the two halves of a surrogate pair (emoji in a translation).
    private static string Fit(string text, int max) =>
        text.Length <= max ? text : text[..(char.IsHighSurrogate(text[max - 1]) ? max - 1 : max)];

    // A new notice replaces the one on screen (one balloon per icon), as the newest status counts.
    // Without the icon they wait for the next AddOrUpdate.
    private void ShowNotices()
    {
        while (_added && _notices.TryPeek(out var notice))
        {
            var data = Data(NOTIFY_ICON_DATA_FLAGS.NIF_INFO);
            data.szInfoTitle = notice.Title;
            data.szInfo = notice.Message;
            data.dwInfoFlags = NOTIFY_ICON_INFOTIP_FLAGS.NIIF_INFO;
            if (PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, data))
            {
                _notices.Shown(notice);
            }
            else
            {
                _notices.Drop(notice);
            }
        }
    }

    public void Dispose()
    {
        PInvoke.PostMessage(_window, PInvoke.WM_CLOSE, default, default);
        _thread.Join(TimeSpan.FromSeconds(2));
        _ready.Dispose();
        s_instance = null;
    }

    private void Run()
    {
        var module = PInvoke.GetModuleHandle((string?)null);
        fixed (char* name = ClassName)
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = s_wndProc,
                hInstance = (HINSTANCE)module.DangerousGetHandle(),
                lpszClassName = name,
            };
            PInvoke.RegisterClassEx(wc);
        }

        _window = PInvoke.CreateWindowEx(0, ClassName, "ZoomIN Tray", 0, 0, 0, 0, 0, HWND.Null, null, module, null);
        _icon = new TrayIconImage(Path.Combine(AppContext.BaseDirectory, "Assets", "ZoomIN.ico")).Load();
        _ready.Set();

        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0) > 0)
        {
            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(msg);
        }
    }

    private void AddOrUpdate()
    {
        var data = Data(NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP | NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE);
        data.szTip = Volatile.Read(ref _menu).Tooltip;
        _added = PInvoke.Shell_NotifyIcon(_added ? NOTIFY_ICON_MESSAGE.NIM_MODIFY : NOTIFY_ICON_MESSAGE.NIM_ADD, data) || _added;
        if (!_added)
        {
            _log.TrayIconFailed();
            return;
        }

        ShowNotices();
    }

    private void ShowMenu()
    {
        if (new TrayPopupMenu(_window).Show(Volatile.Read(ref _menu)) is { } command)
        {
            Raise(command);
        }
    }

    private void Raise(TrayCommand command)
    {
        try
        {
            Command?.Invoke(command);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.TrayCommandFailed(ex, command);
        }
    }

    private void Remove() => PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, Data(0));

    private NOTIFYICONDATAW Data(NOTIFY_ICON_DATA_FLAGS flags) => new()
    {
        cbSize = (uint)sizeof(NOTIFYICONDATAW),
        hWnd = _window,
        uID = IconId,
        uFlags = flags,
        uCallbackMessage = s_callbackMessage,
        hIcon = new HICON((void*)_icon),
    };

    private static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        var self = s_instance;
        if (self is not null)
        {
            if (msg == PInvoke.WM_APP || msg == s_taskbarCreated)
            {
                self._added = msg != s_taskbarCreated && self._added;
                self.AddOrUpdate();
                return default;
            }

            if (msg == s_noticeMessage)
            {
                self.ShowNotices();
                return default;
            }

            if (msg == s_callbackMessage)
            {
                var mouse = (uint)(lParam.Value & 0xFFFF);
                if (mouse == PInvoke.WM_LBUTTONUP)
                {
                    self.Raise(TrayCommand.Open);
                }
                else if (mouse == PInvoke.WM_RBUTTONUP)
                {
                    self.ShowMenu();
                }
                else if (mouse == BalloonUserClick && self._notices.Click is { } command)
                {
                    self.Raise(command);
                }

                return default;
            }

            if (msg == PInvoke.WM_CLOSE)
            {
                self.Remove();
                PInvoke.DestroyWindow(hwnd);
                PInvoke.PostQuitMessage(0);
                return default;
            }
        }

        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }
}
