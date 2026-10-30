using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Hotkeys;

namespace ZoomIN.Platform.Input;

/// <summary>
/// Low-level mouse hook (WH_MOUSE_LL) on its own thread. Windows silently removes hooks whose
/// callback is slow, so the callback only reads the wheel delta, forwards it and returns.
/// </summary>
public sealed unsafe class Win32WheelHook : IMouseWheelHook
{
    private const int WheelDelta = 120;

    // One hook per process; the static callback needs a way back to the instance.
    private static Win32WheelHook? s_active;
    private static readonly HOOKPROC s_proc = HookProc;

    private readonly ILog _log;
    private Thread? _thread;
    private uint _threadId;
    private HotkeyModifiers _modifiers;
    private Action<double>? _onNotches;

    public Win32WheelHook(ILog log)
    {
        _log = log;
    }

    public void Start(HotkeyModifiers modifiers, Action<double> onNotches)
    {
        if (_thread is not null)
        {
            return;
        }

        _modifiers = modifiers;
        _onNotches = onNotches;
        s_active = this;
        using var started = new ManualResetEventSlim();
        _thread = new Thread(() => Run(started)) { IsBackground = true, Name = "ZoomIN Wheel Hook" };
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
        using var hook = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_MOUSE_LL, s_proc, module, 0);
        if (hook.IsInvalid)
        {
            _log.Error("Installing the mouse wheel hook failed; Ctrl+wheel zoom is unavailable.");
        }

        started.Set();
        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0) > 0)
        {
            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(msg);
        }
    }

    private static LRESULT HookProc(int code, WPARAM wParam, LPARAM lParam)
    {
        var self = s_active;
        if (code >= 0 && wParam.Value == PInvoke.WM_MOUSEWHEEL && self is not null && self.ModifiersMatch())
        {
            var info = (MSLLHOOKSTRUCT*)lParam.Value;
            var delta = (short)(info->mouseData >> 16);
            self.Forward(delta / (double)WheelDelta);

            // Swallow the event: the focused application must not zoom as well (ADR-0008).
            return new LRESULT(1);
        }

        return PInvoke.CallNextHookEx(HHOOK.Null, code, wParam, lParam);
    }

    private bool ModifiersMatch() =>
        IsDown(VIRTUAL_KEY.VK_CONTROL) == _modifiers.HasFlag(HotkeyModifiers.Control)
        && IsDown(VIRTUAL_KEY.VK_MENU) == _modifiers.HasFlag(HotkeyModifiers.Alt)
        && IsDown(VIRTUAL_KEY.VK_SHIFT) == _modifiers.HasFlag(HotkeyModifiers.Shift)
        && (IsDown(VIRTUAL_KEY.VK_LWIN) || IsDown(VIRTUAL_KEY.VK_RWIN)) == _modifiers.HasFlag(HotkeyModifiers.Windows);

    private static bool IsDown(VIRTUAL_KEY key) => PInvoke.GetAsyncKeyState((int)key) < 0;

    private void Forward(double notches)
    {
        try
        {
            _onNotches?.Invoke(notches);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // An exception escaping a hook callback would crash the process.
            _log.Error("Wheel zoom handler failed", ex);
        }
    }
}
