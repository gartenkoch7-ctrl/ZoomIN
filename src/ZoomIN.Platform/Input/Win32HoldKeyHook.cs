using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Tracking;

namespace ZoomIN.Platform.Input;

/// <summary>
/// Low-level keyboard hook for the hold key of the highlights (E1), on its own thread and only
/// while some highlight uses it. Separate from <see cref="Win32InputActivityHook"/> so either can
/// change on its own. Only observes: every key is passed on unchanged; no allocation per key.
/// What the key events mean is decided by <see cref="HoldKeyTracker"/>.
/// </summary>
public sealed unsafe class Win32HoldKeyHook : IHoldKeySource
{
    // The real key state is checked this often while the hook runs: key repeat alone is not enough
    // (repeat off for accessibility, a second modifier takes the repeat over), a release can be
    // missed (Win+L, elevated windows) and Windows may drop a slow hook. Well below
    // MarkerVisibility.HoldTimeout; one GetAsyncKeyState 2.5 times a second costs next to nothing.
    private static readonly TimeSpan KeyStatePeriod = TimeSpan.FromMilliseconds(400);

    // One instance per process; the static callback needs a way back to it.
    private static Win32HoldKeyHook? s_active;
    private static readonly HOOKPROC s_keyboardProc = KeyboardProc;

    private readonly ILogger _log;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();
    private readonly Lock _state = new();
    private readonly HoldKeyTracker _tracker = new();
    private LowLevelHookThread? _hook;
    private IHoldKeyHandler? _handler;
    private ITimer? _keyState;

    // Disposing a timer does not wait for a callback already queued: each start is a new session, and
    // a check of an earlier one does nothing.
    private int _session;

    public Win32HoldKeyHook(ILogger<Win32HoldKeyHook> log, TimeProvider? time = null)
    {
        _log = log;
        _time = time ?? TimeProvider.System;
    }

    public bool Start(IHoldKeyHandler handler)
    {
        lock (_gate)
        {
            if (_hook is not null)
            {
                return true;
            }

            // Under the state lock, so a timer callback of an earlier session cannot run in between. A
            // key already held at the start is "down" (not a missed press); the key state check then
            // starts the hold.
            lock (_state)
            {
                _handler = handler;
                _session++;
                _tracker.Start(handler.Key, IsPressed(handler.Key));
            }

            s_active = this;
            _hook = new LowLevelHookThread("ZoomIN Hold Key Hook", (WINDOWS_HOOK_ID.WH_KEYBOARD_LL, s_keyboardProc));
            if (!_hook.AllInstalled)
            {
                // Not kept, so a later Start can try again.
                _hook.Dispose();
                _hook = null;
                s_active = null;
                _log.HoldKeyHookUnavailable();
                return false;
            }

            var session = _session;
            _keyState = _time.CreateTimer(_ => CheckKeyState(session), null, KeyStatePeriod, KeyStatePeriod);
            _log.HoldKeyHookStarted(handler.Key);
            return true;
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (_hook is null)
            {
                return;
            }

            _keyState?.Dispose();
            _keyState = null;
            _hook.Dispose();
            _hook = null;
            s_active = null;

            // A key held while the hook goes away would otherwise count as held for ever. Under the
            // state lock, so a timer callback still running sees the key as up and does nothing.
            lock (_state)
            {
                _tracker.Stop();
                _handler?.HoldKeyUp();
                _handler = null; // a timer callback still queued finds nothing to do
            }

            _log.HoldKeyHookStopped();
        }
    }

    public void Dispose() => Stop();

    // Alt Gr also sets the left Ctrl in the key state (the hook skips its fake event by scan code), so
    // with left Ctrl as hold key a held Alt Gr would otherwise count as a missed press.
    private static bool IsPressed(MarkerHoldKey key) =>
        IsDown(HoldKeyCodes.For(key)) && !(key == MarkerHoldKey.LeftCtrl && IsDown(VIRTUAL_KEY.VK_RMENU));

    private static bool IsDown(VIRTUAL_KEY key) => KeyState.IsDown((int)key);

    private static LRESULT KeyboardProc(int code, WPARAM wParam, LPARAM lParam)
    {
        if (code >= 0 && s_active is { } self)
        {
            var info = (KBDLLHOOKSTRUCT*)lParam.Value;
            var message = (uint)wParam.Value;
            self.OnKey(
                (VIRTUAL_KEY)info->vkCode,
                info->scanCode,
                message is PInvoke.WM_KEYDOWN or PInvoke.WM_SYSKEYDOWN,
                message is PInvoke.WM_KEYUP or PInvoke.WM_SYSKEYUP);
        }

        return PInvoke.CallNextHookEx(HHOOK.Null, code, wParam, lParam);
    }

    // Timer thread. The state lock orders it with the hook callback.
    private void CheckKeyState(int session)
    {
        try
        {
            bool missed;
            lock (_state)
            {
                if (session != _session || _handler is not { } handler)
                {
                    return;
                }

                missed = _tracker.OnKeyState(handler, IsPressed(handler.Key));
            }

            // Logged after the lock: the hook callback waits on it for every key.
            if (missed)
            {
                _log.HoldKeyHookMissedPress();
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.HoldKeyHandlerFailed(ex);
        }
    }

    private void OnKey(VIRTUAL_KEY key, uint scanCode, bool down, bool up)
    {
        try
        {
            // Held only for microseconds; the key state timer and Stop are the other users. The handler
            // is read under the lock, so a callback racing Stop cannot press again after its release.
            lock (_state)
            {
                if (_handler is { } handler)
                {
                    _tracker.OnKey(handler, key, scanCode, down, up);
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // An exception escaping a hook callback would crash the process.
            _log.HoldKeyHandlerFailed(ex);
        }
    }
}
