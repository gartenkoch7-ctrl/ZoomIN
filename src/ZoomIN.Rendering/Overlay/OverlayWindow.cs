using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Rendering.Overlay;

/// <summary>
/// Borderless, click-through, topmost window that hosts the DirectComposition swapchain (ADR-0006).
/// It is excluded from screen capture; if that fails, construction fails and nothing is shown
/// (AGENTS.md 2.4). The window lives on its own thread with a message loop.
/// </summary>
internal sealed unsafe class OverlayWindow : IDisposable
{
    private const string ClassName = "ZoomIN.Overlay";

    // Returned for WM_NCHITTEST so the mouse always reaches the window below.
    private const int HitTransparent = -1;

    private const WINDOW_EX_STYLE ExStyle =
        WINDOW_EX_STYLE.WS_EX_NOREDIRECTIONBITMAP | WINDOW_EX_STYLE.WS_EX_LAYERED |
        WINDOW_EX_STYLE.WS_EX_TRANSPARENT | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW |
        WINDOW_EX_STYLE.WS_EX_NOACTIVATE | WINDOW_EX_STYLE.WS_EX_TOPMOST;

    // Kept in a static field so the GC never collects the delegate Windows calls back into.
    private static readonly WNDPROC s_wndProc = WndProc;
    private static readonly Lock s_classGate = new();
    private static bool s_classRegistered;

    private readonly PhysicalRect _bounds;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _created = new();
    private Exception? _createError;

    public OverlayWindow(PhysicalRect bounds)
    {
        _bounds = bounds;
        _thread = new Thread(Run) { IsBackground = true, Name = "ZoomIN Overlay" };
        _thread.Start();
        _created.Wait();
        if (_createError is not null)
        {
            throw new InvalidOperationException("Overlay window could not be created.", _createError);
        }
    }

    public HWND Handle { get; private set; }

    public void Show() => PInvoke.ShowWindow(Handle, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);

    public void Dispose()
    {
        if (!Handle.IsNull)
        {
            PInvoke.PostMessage(Handle, PInvoke.WM_CLOSE, default, default);
        }

        _thread.Join(TimeSpan.FromSeconds(2));
        _created.Dispose();
    }

    private void Run()
    {
        try
        {
            Handle = Create();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            _createError = ex;
        }
        finally
        {
            _created.Set();
        }

        if (Handle.IsNull)
        {
            return;
        }

        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0) > 0)
        {
            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(msg);
        }
    }

    private HWND Create()
    {
        var module = PInvoke.GetModuleHandle((string?)null);
        RegisterClassOnce((HINSTANCE)module.DangerousGetHandle());

        var hwnd = PInvoke.CreateWindowEx(
            ExStyle,
            ClassName,
            "ZoomIN Overlay",
            WINDOW_STYLE.WS_POPUP,
            (int)_bounds.X,
            (int)_bounds.Y,
            (int)_bounds.Width,
            (int)_bounds.Height,
            HWND.Null,
            null,
            module,
            null);
        if (hwnd.IsNull)
        {
            throw new Win32Exception();
        }

        // Layered windows are invisible until their attributes are set; content comes from DComp.
        PInvoke.SetLayeredWindowAttributes(hwnd, new COLORREF(0), 255, LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);

        if (!PInvoke.SetWindowDisplayAffinity(hwnd, WINDOW_DISPLAY_AFFINITY.WDA_EXCLUDEFROMCAPTURE))
        {
            var error = new Win32Exception();
            PInvoke.DestroyWindow(hwnd);
            throw new InvalidOperationException("Excluding the overlay from screen capture failed.", error);
        }

        return hwnd;
    }

    private static void RegisterClassOnce(HINSTANCE instance)
    {
        lock (s_classGate)
        {
            if (s_classRegistered)
            {
                return;
            }

            fixed (char* name = ClassName)
            {
                var wc = new WNDCLASSEXW
                {
                    // The struct holds a managed delegate, so sizeof() is not allowed here.
                    cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                    lpfnWndProc = s_wndProc,
                    hInstance = instance,
                    lpszClassName = name,
                };
                if (PInvoke.RegisterClassEx(wc) == 0)
                {
                    throw new Win32Exception();
                }
            }

            s_classRegistered = true;
        }
    }

    private static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == PInvoke.WM_NCHITTEST)
        {
            return new LRESULT(HitTransparent);
        }

        if (msg == PInvoke.WM_DESTROY)
        {
            PInvoke.PostQuitMessage(0);
            return new LRESULT(0);
        }

        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }
}
