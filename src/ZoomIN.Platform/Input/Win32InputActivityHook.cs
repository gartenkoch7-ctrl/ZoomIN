using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Pointer;

namespace ZoomIN.Platform.Input;

/// <summary>
/// Low-level mouse and keyboard hooks on their own thread, for the locator triggers and for
/// hiding the pointer while typing. Only observes: every event is passed on unchanged, and the
/// callbacks only forward and return (Windows removes slow hooks).
/// </summary>
public sealed unsafe class Win32InputActivityHook : IInputActivitySource
{
    // One instance per process; the static callbacks need a way back to it.
    private static Win32InputActivityHook? s_active;
    private static readonly HOOKPROC s_mouseProc = MouseProc;
    private static readonly HOOKPROC s_keyboardProc = KeyboardProc;

    private readonly ILog _log;
    private Thread? _thread;
    private uint _threadId;
    private IInputActivityHandler? _handler;

    // Ctrl went down with nothing else pressed; a release now counts as "Ctrl alone".
    private bool _ctrlAlone;

    public Win32InputActivityHook(ILog log)
    {
        _log = log;
    }

    public void Start(IInputActivityHandler handler)
    {
        if (_thread is not null)
        {
            return;
        }

        _handler = handler;
        s_active = this;
        using var started = new ManualResetEventSlim();
        _thread = new Thread(() => Run(started)) { IsBackground = true, Name = "ZoomIN Activity Hook" };
        _thread.Start();
        started.Wait();
    }

    public void Dispose()
    {
        if (_thread is null)
        {
            return;
        }

        PInvoke.PostThreadMessage(_threadId, PInvoke.WM_QUIT, default, default);
        _thread.Join(TimeSpan.FromSeconds(2));
        s_active = null;
    }

    private void Run(ManualResetEventSlim started)
    {
        _threadId = PInvoke.GetCurrentThreadId();
        using var module = PInvoke.GetModuleHandle((string?)null);
        using var mouse = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_MOUSE_LL, s_mouseProc, module, 0);
        using var keyboard = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, s_keyboardProc, module, 0);
        if (mouse.IsInvalid || keyboard.IsInvalid)
        {
            _log.Error("Installing the activity hooks failed; shake/Ctrl locator and hiding while typing are unavailable.");
        }

        started.Set();
        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0) > 0)
        {
            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(msg);
        }
    }

    private static LRESULT MouseProc(int code, WPARAM wParam, LPARAM lParam)
    {
        if (code >= 0 && s_active is { } self)
        {
            var message = (uint)wParam.Value;
            if (message == PInvoke.WM_MOUSEMOVE)
            {
                var info = (MSLLHOOKSTRUCT*)lParam.Value;
                self.OnMove(info->pt.X, info->pt.Y);
            }
            else if (message is PInvoke.WM_LBUTTONDOWN or PInvoke.WM_RBUTTONDOWN or PInvoke.WM_MBUTTONDOWN)
            {
                // Ctrl+click is not "Ctrl alone".
                self._ctrlAlone = false;
            }
        }

        return PInvoke.CallNextHookEx(HHOOK.Null, code, wParam, lParam);
    }

    private static LRESULT KeyboardProc(int code, WPARAM wParam, LPARAM lParam)
    {
        if (code >= 0 && s_active is { } self)
        {
            var info = (KBDLLHOOKSTRUCT*)lParam.Value;
            var message = (uint)wParam.Value;
            var down = message is PInvoke.WM_KEYDOWN or PInvoke.WM_SYSKEYDOWN;
            var up = message is PInvoke.WM_KEYUP or PInvoke.WM_SYSKEYUP;
            self.OnKey((VIRTUAL_KEY)info->vkCode, down, up);
        }

        return PInvoke.CallNextHookEx(HHOOK.Null, code, wParam, lParam);
    }

    private void OnKey(VIRTUAL_KEY key, bool down, bool up)
    {
        var isCtrl = key is VIRTUAL_KEY.VK_CONTROL or VIRTUAL_KEY.VK_LCONTROL or VIRTUAL_KEY.VK_RCONTROL;
        if (isCtrl)
        {
            // Key repeat sends further "down" events while held; only the first one starts.
            if (down && !_ctrlAlone && !AnyOtherModifier())
            {
                _ctrlAlone = true;
            }
            else if (up && _ctrlAlone)
            {
                _ctrlAlone = false;
                Forward(Signal.CtrlTapped);
            }

            return;
        }

        if (!down)
        {
            return;
        }

        _ctrlAlone = false;
        if (!IsModifier(key) && !IsDown(VIRTUAL_KEY.VK_CONTROL) && !IsDown(VIRTUAL_KEY.VK_MENU) && !WindowsDown())
        {
            Forward(Signal.Typed);
        }
    }

    private static bool IsModifier(VIRTUAL_KEY key) => key is VIRTUAL_KEY.VK_SHIFT or VIRTUAL_KEY.VK_LSHIFT or VIRTUAL_KEY.VK_RSHIFT
        or VIRTUAL_KEY.VK_MENU or VIRTUAL_KEY.VK_LMENU or VIRTUAL_KEY.VK_RMENU or VIRTUAL_KEY.VK_LWIN or VIRTUAL_KEY.VK_RWIN
        or VIRTUAL_KEY.VK_CAPITAL;

    private static bool AnyOtherModifier() => IsDown(VIRTUAL_KEY.VK_MENU) || IsDown(VIRTUAL_KEY.VK_SHIFT) || WindowsDown();

    private static bool WindowsDown() => IsDown(VIRTUAL_KEY.VK_LWIN) || IsDown(VIRTUAL_KEY.VK_RWIN);

    private static bool IsDown(VIRTUAL_KEY key) => PInvoke.GetAsyncKeyState((int)key) < 0;

    // Called for every mouse move: no closures, no allocations.
    private void OnMove(int x, int y)
    {
        try
        {
            _handler?.MouseMoved(Stopwatch.GetTimestamp(), x, y);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.Error("Input activity handler failed", ex);
        }
    }

    private void Forward(Signal signal)
    {
        try
        {
            if (signal == Signal.Typed)
            {
                _handler?.Typed();
            }
            else
            {
                _handler?.CtrlTapped();
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // An exception escaping a hook callback would crash the process.
            _log.Error("Input activity handler failed", ex);
        }
    }

    private enum Signal
    {
        Typed,
        CtrlTapped,
    }
}
