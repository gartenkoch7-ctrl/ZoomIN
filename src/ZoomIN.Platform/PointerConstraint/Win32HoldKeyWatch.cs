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
/// the key counts as held: the pointer must never stay locked.
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

    // _gate: starting and stopping (may join the hook thread). _state: the held flag and its
    // notifications, never held while joining, so the hook and the check cannot interleave a
    // press with a release and leave the axis lock held.
    private readonly Lock _gate = new();
    private readonly Lock _state = new();
    private LowLevelHookThread? _thread;
    private ITimer? _timer;
    private Watch? _watch;
    private bool _held;
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
            // Created outside _state: the hook waits on _state, so nothing slow happens under it.
            var timer = _timer ?? _time.CreateTimer(_ => Check(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            lock (_state)
            {
                ReleaseHeld();
                _timer = timer;
                _watch = new Watch(HoldKeyCodes.VirtualKey(key), changed);
            }

            EnsureHook();
            _log.KeyWatchStarted(key);
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            lock (_state)
            {
                ReleaseHeld();
            }

            // A hook Windows refused earlier gets another try (Reset follows a desktop switch).
            if (Volatile.Read(ref _watch) is not null)
            {
                EnsureHook();
            }
        }

        ReportFailures();
    }

    public void Stop()
    {
        lock (_gate)
        {
            ITimer? timer;
            lock (_state)
            {
                if (_watch is null)
                {
                    return;
                }

                ReleaseHeld();
                _watch = null;
                timer = _timer;
                _timer = null;
            }

            timer?.Dispose();
            _thread?.Dispose();
            _thread = null;
            _log.KeyWatchStopped();
        }

        ReportFailures();
    }

    public void Dispose() => Stop();

    /// <summary>One key event as the hook sees it; internal for tests.</summary>
    internal void OnKey(int virtualKey, uint scanCode, bool down, bool up)
    {
        if (scanCode == HoldKeyCodes.AltGrFakeControlScanCode)
        {
            return;
        }

        lock (_state)
        {
            if (_watch is not { } watch || virtualKey != watch.VirtualKey)
            {
                return;
            }

            // Key repeat sends further "down" events while held; only the first one counts.
            if (down && !_held)
            {
                _held = true;
                _timer?.Change(CheckInterval, CheckInterval);
                Notify(watch, true);
            }
            else if (up && _held)
            {
                ReleaseHeld();
            }
        }
    }

    /// <summary>The periodic check while the key counts as held; internal for tests.</summary>
    internal void Check()
    {
        ReportFailures();
        var missed = false;
        lock (_state)
        {
            // The first check runs half a second after the press, long after Windows set the key
            // state: one "not down" is a release the hook never saw.
            if (_watch is { } watch && _held && !_isDown(watch.VirtualKey))
            {
                ReleaseHeld();
                missed = true;
            }
        }

        if (missed)
        {
            _log.ReleaseMissed();
            ReportFailures();
        }
    }

    // Under _gate.
    private void EnsureHook()
    {
        if (!_installHook || _thread is not null)
        {
            return;
        }

        var thread = new LowLevelHookThread("ZoomIN Axis Lock Key", (WINDOWS_HOOK_ID.WH_KEYBOARD_LL, _proc));
        if (thread.AllInstalled)
        {
            _thread = thread;
            return;
        }

        // Not kept: the next Start or Reset tries again.
        thread.Dispose();
        _log.KeyHookUnavailable();
    }

    private void ReportFailures()
    {
        if (Interlocked.Exchange(ref _failures, 0) is > 0 and var failures)
        {
            _log.HandlerFailed(failures);
        }
    }

    // Under _state.
    private void ReleaseHeld()
    {
        if (!_held)
        {
            return;
        }

        _held = false;

        // One more check later, only to report a failed release notification; then it stops.
        _timer?.Change(CheckInterval, Timeout.InfiniteTimeSpan);
        if (_watch is { } watch)
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
                info->scanCode,
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

    private sealed record Watch(int VirtualKey, Action<bool> Changed);
}
