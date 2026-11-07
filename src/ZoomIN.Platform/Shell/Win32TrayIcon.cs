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
/// when Explorer restarts ("TaskbarCreated"). Menu commands are raised on this thread.
/// </summary>
public sealed unsafe class Win32TrayIcon : ITrayIcon
{
    private const string ClassName = "ZoomIN.Tray";
    private const uint IconId = 1;
    private static readonly uint s_callbackMessage = PInvoke.WM_APP + 2;
    private static readonly uint s_taskbarCreated = PInvoke.RegisterWindowMessage("TaskbarCreated");
    private static readonly WNDPROC s_wndProc = WndProc;
    private static Win32TrayIcon? s_instance;

    private readonly ILogger _log;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
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

    public void Notify(string title, string message)
    {
        var data = Data(NOTIFY_ICON_DATA_FLAGS.NIF_INFO);
        data.szInfoTitle = title;
        data.szInfo = message;
        data.dwInfoFlags = NOTIFY_ICON_INFOTIP_FLAGS.NIIF_INFO;
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, data);
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
        }
    }

    private void ShowMenu()
    {
        var menu = PInvoke.CreatePopupMenu();
        try
        {
            foreach (var item in Volatile.Read(ref _menu).Items)
            {
                var flags = item.Text is null ? MENU_ITEM_FLAGS.MF_SEPARATOR
                    : MENU_ITEM_FLAGS.MF_STRING | (item.Checked ? MENU_ITEM_FLAGS.MF_CHECKED : 0);
                fixed (char* text = item.Text)
                {
                    PInvoke.AppendMenu(menu, flags, (nuint)item.Command + 1, text);
                }
            }

            // Required so the menu closes when the user clicks elsewhere.
            PInvoke.SetForegroundWindow(_window);
            PInvoke.GetCursorPos(out var at);
            var chosen = PInvoke.TrackPopupMenu(menu, TRACK_POPUP_MENU_FLAGS.TPM_RETURNCMD | TRACK_POPUP_MENU_FLAGS.TPM_RIGHTBUTTON,
                at.X, at.Y, 0, _window, null);
            PInvoke.PostMessage(_window, PInvoke.WM_NULL, default, default);
            if (chosen.Value > 0)
            {
                Raise((TrayCommand)(chosen.Value - 1));
            }
        }
        finally
        {
            PInvoke.DestroyMenu(menu);
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

    private sealed class TrayIconImage(string path)
    {
        /// <summary>ZoomIN icon next to the exe; Windows' application icon if it is missing.</summary>
        public nint Load()
        {
            var size = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSMICON);
            if (File.Exists(path))
            {
                fixed (char* file = path)
                {
                    var icon = PInvoke.LoadImage(HINSTANCE.Null, file, GDI_IMAGE_TYPE.IMAGE_ICON, size, size, IMAGE_FLAGS.LR_LOADFROMFILE);
                    if (!icon.IsNull)
                    {
                        return (nint)icon.Value;
                    }
                }
            }

            return (nint)PInvoke.LoadIcon(HINSTANCE.Null, (PCWSTR)(char*)32512).Value; // IDI_APPLICATION
        }
    }
}
