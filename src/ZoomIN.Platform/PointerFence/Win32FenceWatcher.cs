using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.PointerFence;

namespace ZoomIN.Platform.PointerFence;

/// <summary>
/// Tells the fence when Windows may have reset ClipCursor or moved the monitors: desktop switch
/// (Win+L, UAC, Ctrl+Alt+Del), session unlock or reconnect, display change, and a check about
/// once a second. A hidden top-level window on its own thread, because WM_DISPLAYCHANGE is only
/// broadcast to top-level windows; it shows nothing. Runs only while the fence is on.
/// </summary>
internal sealed unsafe class Win32FenceWatcher : IDisposable
{
    private const string ClassName = "ZoomIN.PointerFenceWatcher";
    private const uint CheckIntervalMs = 1000;
    private static readonly TimeSpan s_startTimeout = TimeSpan.FromSeconds(5);
    private static readonly nuint s_checkTimer = 1;

    // Kept in static fields so the GC never collects the delegates Windows calls back into; one
    // watcher per process, so the callbacks find it through s_active.
    private static readonly WNDPROC s_wndProc = WndProc;
    private static readonly WINEVENTPROC s_winEvent = OnDesktopSwitch;
    private static readonly Lock s_classGate = new();
    private static bool s_classRegistered;
    private static Win32FenceWatcher? s_active;

    private readonly IFenceEvents _events;
    private readonly Thread _thread;
    private uint _threadId;
    private HWND _window;
    private Exception? _startError;
    private volatile bool _abandoned;
    private bool _disposed;

    public Win32FenceWatcher(IFenceEvents events)
    {
        _events = events;
        Volatile.Write(ref s_active, this);
        var started = new ManualResetEventSlim();
        _thread = new Thread(() => Run(started)) { IsBackground = true, Name = "ZoomIN Pointer Fence Watcher" };
        _thread.Start();

        // Bounded: the caller holds the fence's lock, and the session service may hang. On a
        // timeout the event stays undisposed (the thread still sets it) and the thread ends itself.
        if (!started.Wait(s_startTimeout))
        {
            // Flag first, then WM_QUIT: the thread either sees the flag before its loop or gets the quit.
            _abandoned = true;
            PInvoke.PostThreadMessage(_threadId, PInvoke.WM_QUIT, default, default);
            Interlocked.CompareExchange(ref s_active, null, this);
            throw new TimeoutException("The pointer fence watcher did not start in time.");
        }

        started.Dispose();
        if (_startError is not null)
        {
            Interlocked.CompareExchange(ref s_active, null, this);
            throw new InvalidOperationException("The pointer fence watcher could not start.", _startError);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Only to a living loop: Windows may have given a finished thread's id to another thread.
        if (_thread.IsAlive)
        {
            PInvoke.PostThreadMessage(_threadId, PInvoke.WM_QUIT, default, default);
        }

        // The fence may release itself from one of our own callbacks: then the loop ends after it returns.
        if (Environment.CurrentManagedThreadId != _thread.ManagedThreadId)
        {
            _thread.Join(TimeSpan.FromSeconds(2));
        }

        Interlocked.CompareExchange(ref s_active, null, this);
    }

    private void Run(ManualResetEventSlim started)
    {
        _threadId = PInvoke.GetCurrentThreadId();
        PInvoke.PeekMessage(out _, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_NOREMOVE);
        var desktopHook = HWINEVENTHOOK.Null;
        try
        {
            _window = CreateWindow();

            // Not fatal if missing (e.g. the session service is off): the timer check still re-applies.
            PInvoke.WTSRegisterSessionNotification(_window, PInvoke.NOTIFY_FOR_THIS_SESSION);
            desktopHook = PInvoke.SetWinEventHook(
                PInvoke.EVENT_SYSTEM_DESKTOPSWITCH, PInvoke.EVENT_SYSTEM_DESKTOPSWITCH, HMODULE.Null, s_winEvent, 0, 0, PInvoke.WINEVENT_OUTOFCONTEXT);
            PInvoke.SetTimer(_window, s_checkTimer, CheckIntervalMs, null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Reported to the constructor; escaping here would end ZoomIN from a background thread.
            _startError = ex;
        }
        finally
        {
            started.Set();
        }

        try
        {
            while (_startError is null && !_abandoned && PInvoke.GetMessage(out var msg, HWND.Null, 0, 0) > 0)
            {
                PInvoke.TranslateMessage(msg);
                PInvoke.DispatchMessage(msg);
            }
        }
        finally
        {
            if (!desktopHook.IsNull)
            {
                PInvoke.UnhookWinEvent(desktopHook);
            }

            if (!_window.IsNull)
            {
                PInvoke.KillTimer(_window, s_checkTimer);
                PInvoke.WTSUnRegisterSessionNotification(_window);
                PInvoke.DestroyWindow(_window);
            }
        }
    }

    private static HWND CreateWindow()
    {
        var module = PInvoke.GetModuleHandle((string?)null);
        RegisterClassOnce((HINSTANCE)module.DangerousGetHandle());
        var hwnd = PInvoke.CreateWindowEx(
            WINDOW_EX_STYLE.WS_EX_TOOLWINDOW, ClassName, "ZoomIN Pointer Fence", WINDOW_STYLE.WS_POPUP,
            0, 0, 0, 0, HWND.Null, null, module, null);
        return hwnd.IsNull ? throw new Win32Exception() : hwnd;
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
        if (Volatile.Read(ref s_active) is { } self && hwnd == self._window)
        {
            if (msg == PInvoke.WM_DISPLAYCHANGE)
            {
                self.Raise(FenceTrigger.DisplayChange);
            }
            else if (msg == PInvoke.WM_WTSSESSION_CHANGE && IsBackInSession((uint)wParam.Value))
            {
                self.Raise(FenceTrigger.SessionUnlock);
            }
            else if (msg == PInvoke.WM_TIMER && wParam.Value == s_checkTimer)
            {
                self.Raise(null);
            }
        }

        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private static bool IsBackInSession(uint change) =>
        change is PInvoke.WTS_SESSION_UNLOCK or PInvoke.WTS_CONSOLE_CONNECT or PInvoke.WTS_REMOTE_CONNECT;

    private static void OnDesktopSwitch(HWINEVENTHOOK hook, uint winEvent, HWND hwnd, int idObject, int idChild, uint thread, uint time) =>
        Volatile.Read(ref s_active)?.Raise(FenceTrigger.DesktopSwitch);

    private void Raise(FenceTrigger? trigger)
    {
        try
        {
            if (trigger is { } value)
            {
                _events.OnTrigger(value);
            }
            else
            {
                _events.OnCheck();
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The fence logs and releases on its own; an exception escaping a window procedure
            // would end ZoomIN, which helps nobody.
        }
    }
}
