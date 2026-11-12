using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Shell;

namespace ZoomIN.Platform.Shell;

/// <summary>
/// Screen reader announcements as UI Automation notifications from a hidden window of its own, so
/// they work without an open settings window (overview on/off, M17 B2). The window lives on its
/// own thread, which also answers the screen reader's questions about it; started on first use.
/// </summary>
public sealed unsafe class UiaAnnouncer : IAnnouncer, IDisposable
{
    private const string ClassName = "ZoomIN.Announcer";
    private static readonly WNDPROC s_wndProc = WndProc;
    private static UiaAnnouncer? s_instance;

    private readonly ILogger _log;
    private readonly ConcurrentQueue<string> _pending = new();
    private readonly Lock _gate = new();
    private Thread? _thread;
    private nint _window;
    private volatile bool _disposed;

    public UiaAnnouncer(ILogger<UiaAnnouncer> log)
    {
        _log = log;
    }

    public void Announce(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Nobody listens: nothing to do, no window needed. ZoomIN's own focus tracking is a UIA
        // client too, so this mainly saves the work while tracking is off.
        if (!PInvoke.UiaClientsAreListening() || !EnsureThread())
        {
            return;
        }

        // Never waits: callers hold locks (overview). Until the window exists the thread raises
        // what is queued right after creating it (it publishes the window before draining).
        _pending.Enqueue(message);
        if (Window is { IsNull: false } window)
        {
            PInvoke.PostMessage(window, PInvoke.WM_APP, default, default);
        }
    }

    private HWND Window => new((void*)Volatile.Read(ref _window));

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            if (_thread is null || Window.IsNull)
            {
                // A thread still creating its window sees _disposed and ends itself.
                Interlocked.CompareExchange(ref s_instance, null, this);
                return;
            }
        }

        PInvoke.PostMessage(Window, PInvoke.WM_CLOSE, default, default);
        _thread.Join(TimeSpan.FromSeconds(2));
        Interlocked.CompareExchange(ref s_instance, null, this);
    }

    private bool EnsureThread()
    {
        lock (_gate)
        {
            if (_thread is null && !_disposed)
            {
                s_instance = this;
                _thread = new Thread(Run) { IsBackground = true, Name = "ZoomIN Announcer" };
                _thread.Start();
            }

            return !_disposed;
        }
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

        // Never shown: a tool window without size, only a source for the notifications.
        var window = PInvoke.CreateWindowEx(WINDOW_EX_STYLE.WS_EX_TOOLWINDOW, ClassName, "ZoomIN", WINDOW_STYLE.WS_POPUP, 0, 0, 0, 0, HWND.Null, null, module, null);
        if (window.IsNull)
        {
            // Unusable for good: stop queueing announcements nobody will raise.
            _disposed = true;
            _pending.Clear();
            _log.AnnouncerUnavailable("window");
            return;
        }

        // Under the lock, so Dispose either sees the window (and closes it) or this sees Dispose.
        lock (_gate)
        {
            if (_disposed)
            {
                PInvoke.DestroyWindow(window);
                return;
            }

            Volatile.Write(ref _window, (nint)window.Value);
        }

        RaisePending(window);
        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0) > 0)
        {
            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(msg);
        }
    }

    private void RaisePending(HWND hwnd)
    {
        while (_pending.TryDequeue(out var message))
        {
            var text = Marshal.StringToBSTR(message);
            var activity = Marshal.StringToBSTR("ZoomIN.Status");
            IRawElementProviderSimple? provider = null;
            try
            {
                PInvoke.UiaHostProviderFromHwnd(hwnd, out provider).ThrowOnFailure();
                PInvoke.UiaRaiseNotificationEvent(
                    provider, NotificationKind.NotificationKind_ActionCompleted, NotificationProcessing.NotificationProcessing_ImportantMostRecent,
                    new BSTR((char*)text), new BSTR((char*)activity)).ThrowOnFailure();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Inside a window procedure nothing may escape. The type only: an announcement is a
                // nicety, never worth more than a warning.
                _log.AnnouncerUnavailable(ex.GetType().Name);
            }
            finally
            {
                // Released now rather than by the finalizer: it holds UIA references to the window.
                if (provider is not null && Marshal.IsComObject(provider))
                {
                    Marshal.ReleaseComObject(provider);
                }

                Marshal.FreeBSTR(text);
                Marshal.FreeBSTR(activity);
            }
        }
    }

    private static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (s_instance is { } self)
        {
            if (msg == PInvoke.WM_APP)
            {
                self.RaisePending(hwnd);
                return default;
            }

            if (msg == PInvoke.WM_CLOSE)
            {
                PInvoke.DestroyWindow(hwnd);
                PInvoke.PostQuitMessage(0);
                return default;
            }
        }

        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }
}
