using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.Tests.WindowFilters;

/// <summary>
/// A solid white or black top-level window of the test itself, on its own thread with a message
/// loop. Topmost and never activated, so it does not take the focus from whatever is open.
/// </summary>
internal sealed unsafe class TestWindow : IDisposable
{
    public const string WhiteClass = "ZoomIN.Test.White";
    public const string BlackClass = "ZoomIN.Test.Black";

    private static readonly WNDPROC s_wndProc = WndProc;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private HWND _hwnd;

    private TestWindow(bool white, PhysicalRect bounds, nint owner)
    {
        _thread = new Thread(() => Run(white, bounds, (HWND)owner)) { IsBackground = true, Name = "Test window" };
        _thread.Start();
        _ready.Wait(TimeSpan.FromSeconds(5));
    }

    public nint Handle => _hwnd;

    public static TestWindow White(PhysicalRect bounds, nint owner = 0) => new(true, bounds, owner);

    public static TestWindow Black(PhysicalRect bounds, nint owner = 0) => new(false, bounds, owner);

    public void Hide() => PInvoke.ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_HIDE);

    public void Show() => PInvoke.ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);

    public void Dispose()
    {
        PInvoke.PostMessage(_hwnd, PInvoke.WM_CLOSE, default, default);
        _thread.Join(TimeSpan.FromSeconds(5));
        _ready.Dispose();
    }

    private void Run(bool white, PhysicalRect bounds, HWND owner)
    {
        var className = white ? WhiteClass : BlackClass;
        var module = PInvoke.GetModuleHandle((string?)null);
        fixed (char* name = className)
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = s_wndProc,
                hInstance = (HINSTANCE)module.DangerousGetHandle(),
                lpszClassName = name,
                hbrBackground = (HBRUSH)(nint)PInvoke.GetStockObject(white ? GET_STOCK_OBJECT_FLAGS.WHITE_BRUSH : GET_STOCK_OBJECT_FLAGS.BLACK_BRUSH),
            };

            // A second window of the same colour finds the class registered already.
            PInvoke.RegisterClassEx(wc);
        }

        _hwnd = PInvoke.CreateWindowEx(
            WINDOW_EX_STYLE.WS_EX_TOPMOST | WINDOW_EX_STYLE.WS_EX_NOACTIVATE,
            className, "ZoomIN test window",
            WINDOW_STYLE.WS_POPUP | WINDOW_STYLE.WS_VISIBLE,
            (int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height,
            owner, null, module, null);
        PInvoke.UpdateWindow(_hwnd);
        _ready.Set();

        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0) > 0)
        {
            PInvoke.DispatchMessage(msg);
        }
    }

    private static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == 0x0002) // WM_DESTROY
        {
            PInvoke.PostQuitMessage(0);
        }

        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }
}
