using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Pointer;

namespace ZoomIN.Platform.Input;

/// <summary>
/// Low-level mouse and keyboard hooks on their own thread, for the locator triggers and for
/// hiding the pointer while typing. Only observes: every event is passed on unchanged (except Esc
/// while the overview runs, see <see cref="OverviewKeyWatch"/>, and clicks, wheel, Esc and Del
/// while islands are edited, see <see cref="IslandEditCapture"/>), and the callbacks only forward
/// and return (Windows removes slow hooks).
/// </summary>
public sealed unsafe class Win32InputActivityHook : IInputActivitySource
{
    // One instance per process; the static callbacks need a way back to it.
    private static Win32InputActivityHook? s_active;
    private static readonly HOOKPROC s_mouseProc = MouseProc;
    private static readonly HOOKPROC s_keyboardProc = KeyboardProc;

    private readonly ILogger _log;
    private readonly OverviewKeyWatch _overviewKeys;
    private readonly IslandEditCapture _islandEdit;
    private LowLevelHookThread? _hooks;
    private IInputActivityHandler? _handler;

    // Ctrl went down with nothing else pressed; a release now counts as "Ctrl alone".
    private bool _ctrlAlone;

    public Win32InputActivityHook(ILogger<Win32InputActivityHook> log, OverviewKeyWatch overviewKeys, IslandEditCapture islandEdit)
    {
        _log = log;
        _overviewKeys = overviewKeys;
        _islandEdit = islandEdit;
    }

    public void Start(IInputActivityHandler handler)
    {
        if (_hooks is not null)
        {
            return;
        }

        _handler = handler;
        s_active = this;
        _hooks = new LowLevelHookThread(
            "ZoomIN Activity Hook", (WINDOWS_HOOK_ID.WH_MOUSE_LL, s_mouseProc), (WINDOWS_HOOK_ID.WH_KEYBOARD_LL, s_keyboardProc));
        _islandEdit.HooksInstalled(_hooks.AllInstalled);
        if (!_hooks.AllInstalled)
        {
            _log.ActivityHooksUnavailable();
        }
    }

    public void Dispose()
    {
        if (_hooks is null)
        {
            return;
        }

        _islandEdit.HooksInstalled(false);
        _hooks.Dispose();
        s_active = null;
    }

    private static LRESULT MouseProc(int code, WPARAM wParam, LPARAM lParam)
    {
        if (code >= 0 && s_active is { } self)
        {
            var message = (uint)wParam.Value;
            var info = (MSLLHOOKSTRUCT*)lParam.Value;

            // Island edit mode (B1): its clicks and wheel never reach applications.
            if (self.SwallowForIslandEdit(message, info))
            {
                return (LRESULT)1;
            }

            if (message == PInvoke.WM_MOUSEMOVE)
            {
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

            // Keys this hook swallows: Esc and Del while islands are edited (B1), Esc while the overview runs (B2).
            if (self.SwallowForIslandEdit((int)info->vkCode, down, up, info->time) || self.SwallowForOverview((int)info->vkCode, down, up, info->time))
            {
                // Ctrl+Esc is not "Ctrl alone", even though OnKey never sees the Esc.
                self._ctrlAlone = false;
                return (LRESULT)1;
            }

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
        if (!ModifierKeys.Is(key) && !IsDown(VIRTUAL_KEY.VK_CONTROL) && !IsDown(VIRTUAL_KEY.VK_MENU) && !WindowsDown())
        {
            Forward(Signal.Typed);
        }
    }

    private static bool AnyOtherModifier() => IsDown(VIRTUAL_KEY.VK_MENU) || IsDown(VIRTUAL_KEY.VK_SHIFT) || WindowsDown();

    private static bool WindowsDown() => IsDown(VIRTUAL_KEY.VK_LWIN) || IsDown(VIRTUAL_KEY.VK_RWIN);

    private static bool IsDown(VIRTUAL_KEY key) => KeyState.IsDown((int)key);

    // Called for every mouse move: no closures, no allocations.
    private void OnMove(int x, int y)
    {
        try
        {
            _handler?.MouseMoved(Stopwatch.GetTimestamp(), x, y);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.ActivityHandlerFailed(ex);
        }
    }

    private bool SwallowForIslandEdit(uint message, MSLLHOOKSTRUCT* info)
    {
        try
        {
            return _islandEdit.OnMouse(message, info->pt.X, info->pt.Y, info->mouseData);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // An exception escaping a hook callback would crash the process.
            _log.ActivityHandlerFailed(ex);
            return false;
        }
    }

    private bool SwallowForIslandEdit(int key, bool down, bool up, uint time)
    {
        try
        {
            return _islandEdit.OnKey(key, down, up, time);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.ActivityHandlerFailed(ex);
            return false;
        }
    }

    private bool SwallowForOverview(int key, bool down, bool up, uint time)
    {
        try
        {
            return _overviewKeys.OnKey(key, down, up, time);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // An exception escaping a hook callback would crash the process.
            _log.OverviewKeyHandlerFailed(ex);
            return false;
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
            _log.ActivityHandlerFailed(ex);
        }
    }

    private enum Signal
    {
        Typed,
        CtrlTapped,
    }
}
