using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.Pointer;
using ZoomIN.Platform.Input;

namespace ZoomIN.Platform.PointerConstraint;

/// <summary>
/// The held key of the axis lock (A6): a read-only low-level keyboard hook on its own thread, only
/// while the axis lock is on; left and right keys apart, nothing swallowed. A release Windows never
/// delivered (lock screen, UAC) is caught by a check of the physical key state twice a second while
/// the key counts as held: the pointer must never stay locked. The held state is <see cref="HeldKeyState"/>.
/// </summary>
public sealed unsafe class Win32HoldKeyWatch : IHoldKeyWatch
{
    internal static readonly TimeSpan CheckInterval = TimeSpan.FromMilliseconds(500);

    private readonly ILogger _log;
    private readonly TimeProvider _time;
    private readonly HeldKeyState _key;
    private readonly bool _installHook;

    // Kept in a field so the GC never collects the callback while Windows holds it.
    private readonly HOOKPROC _proc;

    // Starting and stopping; may join the hook thread, so the hook never waits on it.
    private readonly Lock _gate = new();
    private LowLevelHookThread? _thread;
    private ITimer? _timer;

    public Win32HoldKeyWatch(ILogger<Win32HoldKeyWatch> log, TimeProvider time)
        : this(log, time, KeyState.IsDown, installHook: true)
    {
    }

    /// <summary>For tests: without a real hook and with a fake key state.</summary>
    internal Win32HoldKeyWatch(ILogger log, TimeProvider time, Func<int, bool> isDown, bool installHook)
    {
        _log = log;
        _time = time;
        _key = new HeldKeyState(isDown, CheckInterval);
        _installHook = installHook;
        _proc = KeyboardProc;
    }

    public void Start(AxisLockKey key, Action<bool> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        lock (_gate)
        {
            using (ExecutionContext.SuppressFlow())
            {
                // Without the caller's context: later checks must not log under its ended activity.
                _timer ??= _time.CreateTimer(_ => Check(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }

            _key.Follow(HoldKeyCodes.VirtualKey(key), changed, _timer);
            if (EnsureHook())
            {
                _log.KeyWatchStarted(key);
            }
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _key.ReleaseNow();

            // A hook Windows refused earlier gets another try (Reset follows a desktop switch).
            if (_key.IsWatching)
            {
                _ = EnsureHook();
            }
        }

        ReportFailures();
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!_key.IsWatching)
            {
                return;
            }

            _key.Clear()?.Dispose();
            _timer = null;
            _thread?.Dispose();
            _thread = null;
            _log.KeyWatchStopped();
        }

        ReportFailures();
    }

    public void Dispose() => Stop();

    /// <summary>One key event as the hook sees it; internal for tests.</summary>
    internal void OnKey(int virtualKey, uint scanCode, bool extended, bool down, bool up, bool injected = false) =>
        _key.OnKey(virtualKey, scanCode, extended, down, up, injected);

    /// <summary>The periodic check while the key counts as held; internal for tests.</summary>
    internal void Check()
    {
        ReportFailures();
        if (_key.ReleaseIfMissed())
        {
            _log.ReleaseMissed();
            ReportFailures();
        }
    }

    // Under _gate. False if Windows refused the hook (logged).
    private bool EnsureHook()
    {
        if (!_installHook || _thread is not null)
        {
            return true;
        }

        LowLevelHookThread thread;
        using (ExecutionContext.SuppressFlow())
        {
            thread = new LowLevelHookThread("ZoomIN Axis Lock Key", (WINDOWS_HOOK_ID.WH_KEYBOARD_LL, _proc));
        }

        if (thread.AllInstalled)
        {
            _thread = thread;
            return true;
        }

        // Not kept: the next Start or Reset tries again.
        thread.Dispose();
        _log.KeyHookUnavailable();
        return false;
    }

    private void ReportFailures()
    {
        if (_key.TakeFailures() is > 0 and var failures)
        {
            _log.HandlerFailed(failures);
        }
    }

    private LRESULT KeyboardProc(int code, WPARAM wParam, LPARAM lParam)
    {
        if (code >= 0)
        {
            var info = (KBDLLHOOKSTRUCT*)lParam.Value;
            var message = (uint)wParam.Value;
            _key.OnKey(
                (int)info->vkCode,
                info->scanCode,
                (info->flags & KBDLLHOOKSTRUCT_FLAGS.LLKHF_EXTENDED) != 0,
                message is PInvoke.WM_KEYDOWN or PInvoke.WM_SYSKEYDOWN,
                message is PInvoke.WM_KEYUP or PInvoke.WM_SYSKEYUP,
                (info->flags & KBDLLHOOKSTRUCT_FLAGS.LLKHF_INJECTED) != 0);
        }

        return PInvoke.CallNextHookEx(HHOOK.Null, code, wParam, lParam);
    }
}
