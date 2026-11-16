using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using Microsoft.Extensions.Logging;
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

    private readonly ILogger _log;
    private LowLevelHookThread? _hooks;
    private HotkeyModifiers _modifiers;
    private Action<double>? _onNotches;

    public Win32WheelHook(ILogger<Win32WheelHook> log)
    {
        _log = log;
    }

    public void Start(HotkeyModifiers modifiers, Action<double> onNotches)
    {
        if (_hooks is not null)
        {
            return;
        }

        _modifiers = modifiers;
        _onNotches = onNotches;
        s_active = this;
        _hooks = new LowLevelHookThread("ZoomIN Wheel Hook", (WINDOWS_HOOK_ID.WH_MOUSE_LL, s_proc));
        if (!_hooks.AllInstalled)
        {
            _log.WheelHookUnavailable();
        }
    }

    public void Dispose()
    {
        if (_hooks is null)
        {
            return;
        }

        _hooks.Dispose();
        s_active = null;
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
            _log.WheelHandlerFailed(ex);
        }
    }
}
