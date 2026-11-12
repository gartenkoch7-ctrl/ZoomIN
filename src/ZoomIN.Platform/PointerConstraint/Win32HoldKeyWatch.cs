using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.Pointer;
using ZoomIN.Platform.Input;

namespace ZoomIN.Platform.PointerConstraint;

/// <summary>
/// The held key of the axis lock (A6): a read-only low-level keyboard hook on its own thread, so
/// left and right keys are told apart and nothing is swallowed. The hook only flips a flag.
/// A release Windows never delivered (key let go on the lock screen or a UAC prompt) is caught by
/// a check of the physical key state twice a second: the pointer must never stay locked.
/// </summary>
public sealed unsafe class Win32HoldKeyWatch : IHoldKeyWatch
{
    internal static readonly TimeSpan CheckInterval = TimeSpan.FromMilliseconds(500);

    private readonly ILogger _log;
    private readonly TimeProvider _time;
    private readonly Func<int, bool> _isDown;
    private readonly bool _installHook;

    // Kept in a field so the GC never collects the callback while Windows holds it.
    private readonly HOOKPROC _proc;
    private readonly Lock _gate = new();
    private LowLevelHookThread? _thread;
    private ITimer? _timer;
    private Watch? _watch;

    // 1 while the key is held; changed by compare-exchange from the hook and the check timer.
    private int _held;
    private int _missedChecks;
    private int _failures;

    public Win32HoldKeyWatch(ILogger<Win32HoldKeyWatch> log, TimeProvider time)
        : this(log, time, KeyState.IsDown, installHook: true)
    {
    }

    /// <summary>For tests: without a real hook and with a fake key state.</summary>
    internal Win32HoldKeyWatch(ILogger log, TimeProvider time, Func<int, bool> isDown, bool installHook)
    {
        _log = log;
        _time = time;
        _isDown = isDown;
        _installHook = installHook;
        _proc = KeyboardProc;
    }

    public void Start(AxisLockKey key, Action<bool> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        lock (_gate)
        {
            Release(Volatile.Read(ref _watch));
            Volatile.Write(ref _watch, new Watch(VirtualKey(key), changed));
            if (_installHook && _thread is null)
            {
                _thread = new LowLevelHookThread("ZoomIN Axis Lock Key", (WINDOWS_HOOK_ID.WH_KEYBOARD_LL, _proc));
                if (!_thread.AllInstalled)
                {
                    _log.KeyHookUnavailable();
                }
            }

            _timer ??= _time.CreateTimer(_ => Check(), null, CheckInterval, CheckInterval);
            _log.KeyWatchStarted(key);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            var watch = Volatile.Read(ref _watch);
            if (watch is null)
            {
                return;
            }

            Volatile.Write(ref _watch, null);
            _thread?.Dispose();
            _thread = null;
            _timer?.Dispose();
            _timer = null;
            Release(watch);
            _log.KeyWatchStopped();
        }
    }

    public void Dispose() => Stop();

    /// <summary>One key event as the hook sees it; internal for tests.</summary>
    internal void OnKey(int virtualKey, bool down, bool up)
    {
        if (Volatile.Read(ref _watch) is not { } watch || virtualKey != watch.VirtualKey)
        {
            return;
        }

        // Key repeat sends further "down" events while held; only the first one counts.
        if (down && Interlocked.CompareExchange(ref _held, 1, 0) == 0)
        {
            Volatile.Write(ref _missedChecks, 0);
            Notify(watch, true);
        }
        else if (up && Interlocked.CompareExchange(ref _held, 0, 1) == 1)
        {
            Notify(watch, false);
        }
    }

    /// <summary>The periodic check; internal for tests.</summary>
    internal void Check()
    {
        if (Interlocked.Exchange(ref _failures, 0) is > 0 and var failures)
        {
            _log.HandlerFailed(failures);
        }

        if (Volatile.Read(ref _watch) is not { } watch || Volatile.Read(ref _held) == 0)
        {
            return;
        }

        if (_isDown(watch.VirtualKey))
        {
            Volatile.Write(ref _missedChecks, 0);
            return;
        }

        // Twice in a row: right after a press the hook runs before Windows updates the key state.
        if (Interlocked.Increment(ref _missedChecks) >= 2 && Interlocked.CompareExchange(ref _held, 0, 1) == 1)
        {
            Notify(watch, false);
            _log.ReleaseMissed();
        }
    }

    private void Release(Watch? watch)
    {
        if (watch is not null && Interlocked.Exchange(ref _held, 0) == 1)
        {
            Notify(watch, false);
        }
    }

    private LRESULT KeyboardProc(int code, WPARAM wParam, LPARAM lParam)
    {
        if (code >= 0)
        {
            var info = (KBDLLHOOKSTRUCT*)lParam.Value;
            var message = (uint)wParam.Value;
            OnKey(
                (int)info->vkCode,
                message is PInvoke.WM_KEYDOWN or PInvoke.WM_SYSKEYDOWN,
                message is PInvoke.WM_KEYUP or PInvoke.WM_SYSKEYUP);
        }

        return PInvoke.CallNextHookEx(HHOOK.Null, code, wParam, lParam);
    }

    private void Notify(Watch watch, bool held)
    {
        try
        {
            watch.Changed(held);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // An exception escaping a hook callback would end ZoomIN; the check reports it.
            Interlocked.Increment(ref _failures);
        }
    }

    internal static int VirtualKey(AxisLockKey key) => (int)(key switch
    {
        AxisLockKey.RightShift => VIRTUAL_KEY.VK_RSHIFT,
        AxisLockKey.LeftControl => VIRTUAL_KEY.VK_LCONTROL,
        AxisLockKey.RightControl => VIRTUAL_KEY.VK_RCONTROL,
        AxisLockKey.LeftAlt => VIRTUAL_KEY.VK_LMENU,
        AxisLockKey.RightAlt => VIRTUAL_KEY.VK_RMENU,
        _ => VIRTUAL_KEY.VK_LSHIFT,
    });

    private sealed record Watch(int VirtualKey, Action<bool> Changed);
}
