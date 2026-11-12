using System.Diagnostics.Metrics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Tracking;

namespace ZoomIN.Platform.Input;

/// <summary>
/// Low-level keyboard hook for the hold key of the highlights (E1), on its own thread and only
/// while some highlight uses it. Separate from <see cref="Win32InputActivityHook"/> so either can
/// change on its own. Only observes: every key is passed on unchanged; no allocation per key.
/// </summary>
public sealed unsafe class Win32HoldKeyHook : IHoldKeySource
{
    private static readonly Counter<long> s_presses = Telemetry.Meter.CreateCounter<long>(
        "zoomin.highlight.hold_key_presses", "{press}", "Hold key pressed for the highlights (no key content)");

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
    private LowLevelHookThread? _hook;
    private IHoldKeyHandler? _handler;
    private ITimer? _keyState;
    private bool _down;
    private bool _missedLogged;
    private bool _suspect;
    private MarkerHoldKey _downKey;

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
                _downKey = handler.Key;
                _down = PInvoke.GetAsyncKeyState((int)HoldKeyCodes.For(handler.Key)) < 0;
                _suspect = false;
                _missedLogged = false;
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

            _keyState = _time.CreateTimer(_ => CheckKeyState(), null, KeyStatePeriod, KeyStatePeriod);
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
            // state lock, so a timer callback still running sees _down = false and does nothing.
            lock (_state)
            {
                _down = false;
                _suspect = false;
                _handler?.HoldKeyUp();
                _handler = null; // a timer callback still queued finds nothing to do
            }

            _log.HoldKeyHookStopped();
        }
    }

    public void Dispose() => Stop();

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

    // Timer thread: refreshes a running hold while the key is really down, ends it otherwise.
    // The state lock orders it with the hook callback; the lag between a release and
    // the key state is covered by the second-check rule below.
    private void CheckKeyState()
    {
        try
        {
            lock (_state)
            {
                if (_handler is not { } handler)
                {
                    return;
                }

                var pressed = PInvoke.GetAsyncKeyState((int)HoldKeyCodes.For(handler.Key)) < 0;
                if (pressed && !_down)
                {
                    // Down although the hook saw no press: Windows removed the hook (too slow under
                    // load). Only on the second check in a row – right after a release the key state
                    // may still say "down", since Windows updates it after the hooks returned.
                    if (!_suspect)
                    {
                        _suspect = true;
                        return;
                    }

                    if (!_missedLogged)
                    {
                        _missedLogged = true;
                        _log.HoldKeyHookMissedPress();
                    }

                    _down = true;
                }

                _suspect = false;
                if (pressed)
                {
                    handler.HoldKeyDown();
                }
                else if (_down)
                {
                    _down = false;
                    handler.HoldKeyUp();
                }
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
                    OnKeyLocked(handler, key, scanCode, down, up);
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // An exception escaping a hook callback would crash the process.
            _log.HoldKeyHandlerFailed(ex);
        }
    }

    private void OnKeyLocked(IHoldKeyHandler handler, VIRTUAL_KEY key, uint scanCode, bool down, bool up)
    {
        // A new hold key: the old one's release will not come, so its press no longer blocks counting.
        var holdKey = handler.Key;
        _suspect = false; // the hook is alive
        if (holdKey != _downKey)
        {
            (_down, _downKey) = (false, holdKey);
        }

        switch (HoldKeyCodes.Classify(key, scanCode, down, up, holdKey))
        {
            case HoldKeyEvent.Down:
                // Key repeat sends further "down" events while held; count the press once.
                if (!_down)
                {
                    _down = true;
                    s_presses.Add(1);
                }

                handler.HoldKeyDown();
                break;
            case HoldKeyEvent.Up:
                _down = false;
                handler.HoldKeyUp();
                break;
            case HoldKeyEvent.Other:
                handler.OtherKeyDown();
                break;
        }
    }
}
