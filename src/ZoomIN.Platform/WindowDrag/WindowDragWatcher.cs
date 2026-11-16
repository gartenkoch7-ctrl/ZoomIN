using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.WindowDrag;

namespace ZoomIN.Platform.WindowDrag;

/// <summary>
/// Out-of-context WinEvent hooks of the drag thread (G1): move/size start and end of any top-level
/// window, and the location of the dragged window while its drag lasts. When a loop counts as a
/// drag decides <see cref="DragStartProbe"/>. Created, used and disposed on that thread; the
/// static callback only forwards (ADR-0025).
/// </summary>
internal sealed unsafe class WindowDragWatcher : IDisposable
{
    private const uint EventMoveSizeStart = 0x000A;
    private const uint EventMoveSizeEnd = 0x000B;
    private const uint EventLocationChange = 0x800B;

    /// <summary>Safety check: a window that died mid-drag sends no end event.</summary>
    private const uint CheckIntervalMs = 250;

    /// <summary>Outline dragging has no location events: the timer watches the pointer instead.</summary>
    private const uint OutlineIntervalMs = 50;

    /// <summary>The loop has been over this long without an end event: the window is gone.</summary>
    private const uint LostAfterMs = 500;

    // One drag thread per process; the static callback finds its watcher here.
    private static WindowDragWatcher? s_current;
    private static readonly WINEVENTPROC s_callback = OnEvent;

    private readonly Action<WindowDragSignal> _signal;
    private readonly Func<bool> _active;
    private readonly ILogger _log;
    private readonly TimeProvider _time;
    private readonly HWINEVENTHOOK _moveSizeHook;
    private readonly DragStartProbe _probe = new();
    private readonly DragStartReport _startLog;
    private HWINEVENTHOOK _locationHook;
    private HWND _window;
    private uint _windowThread;
    private uint _windowProcess;
    private bool _started;
    private HMONITOR _monitor;
    private nuint _timer;
    private long _startedAt;
    private int _missedChecks;

    /// <param name="signal">Receives the drag steps.</param>
    /// <param name="active">False while the feature cannot act: then no window is probed at all.</param>
    /// <param name="log">Log of the source.</param>
    /// <param name="time">Clock for the drag duration in the log.</param>
    public WindowDragWatcher(Action<WindowDragSignal> signal, Func<bool> active, ILogger log, TimeProvider time)
    {
        _signal = signal;
        _active = active;
        _log = log;
        _time = time;
        _startLog = new DragStartReport(log);
        s_current = this;
        _moveSizeHook = PInvoke.SetWinEventHook(EventMoveSizeStart, EventMoveSizeEnd, HMODULE.Null, s_callback, 0, 0, 0);
    }

    /// <summary>False if Windows refused the hook; then no drag is ever reported.</summary>
    public bool IsHooked => _moveSizeHook != HWINEVENTHOOK.Null;

    /// <summary>WM_TIMER of the drag thread.</summary>
    public void OnTimer(nuint timer)
    {
        if (timer != _timer || _window == HWND.Null)
        {
            return;
        }

        if (MoveSizeLoop.IsRunning(_windowThread, _window))
        {
            // Also covers outline dragging and windows whose location events do not arrive.
            _missedChecks = 0;
            WindowChanged(polled: true);
        }
        else if (++_missedChecks * (_probe.OutlineOnly ? OutlineIntervalMs : CheckIntervalMs) >= LostAfterMs)
        {
            // Not at once: right after a normal release the end event may still be on its way.
            End("lost");
        }
    }

    public void Dispose()
    {
        if (_window != HWND.Null)
        {
            End("stopped");
        }

        if (IsHooked)
        {
            PInvoke.UnhookWinEvent(_moveSizeHook);
        }

        s_current = null;
    }

    private static void OnEvent(HWINEVENTHOOK hook, uint @event, HWND hwnd, int idObject, int idChild, uint thread, uint time)
    {
        // Only whole windows; carets and controls report location changes too.
        if (s_current is not { } watcher || hwnd == HWND.Null || idObject != 0 || idChild != 0)
        {
            return;
        }

        try
        {
            watcher.Handle(@event, hwnd);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // An exception must not leave the native callback.
            watcher._log.EventFailed(ex);
        }
    }

    private void Handle(uint @event, HWND hwnd)
    {
        if (@event == EventMoveSizeStart)
        {
            if (PInvoke.GetAncestor(hwnd, GET_ANCESTOR_FLAGS.GA_ROOT) == hwnd)
            {
                Begin(hwnd);
            }
        }
        else if (hwnd != _window)
        {
            return;
        }
        else if (@event == EventMoveSizeEnd)
        {
            End("released");
        }
        else if (@event == EventLocationChange)
        {
            WindowChanged(polled: false);
        }
    }

    private void Begin(HWND hwnd)
    {
        if (_window != HWND.Null)
        {
            End("restarted");
        }

        // Nothing to do (zoom off, Docked, "unchanged", both switches off): no hit test, location
        // hook or timer. Decided at the press: switching zoom on while holding it skips this drag.
        if (!_active())
        {
            return;
        }

        uint processId;
        var thread = PInvoke.GetWindowThreadProcessId(hwnd, &processId);
        if (thread == 0)
        {
            // Gone before the late event arrived; 0 would hook location events of every process.
            return;
        }

        _windowThread = thread;
        _windowProcess = processId;
        _window = hwnd;
        _started = false;
        _missedChecks = 0;
        _probe.Begin(hwnd);

        // Location events only of this window's thread, and only while it is dragged.
        _locationHook = PInvoke.SetWinEventHook(EventLocationChange, EventLocationChange, HMODULE.Null, s_callback, processId, _windowThread, 0);
        _timer = PInvoke.SetTimer(HWND.Null, 0, _probe.OutlineOnly ? OutlineIntervalMs : CheckIntervalMs, null);
    }

    private void WindowChanged(bool polled)
    {
        if (!_started)
        {
            if (!_probe.TryStart(polled, out var kind))
            {
                return;
            }

            _started = true;
            _startedAt = _time.GetTimestamp(); // the drag counts from its first change
            _monitor = MoveSizeLoop.MonitorUnderPointer();
            _signal(new WindowDragSignal(WindowDragStage.Started, kind)); // first: the log reads names
            _startLog.Started(_window, _windowProcess, kind, _probe);
            return;
        }

        var monitor = MoveSizeLoop.MonitorUnderPointer();
        if (monitor != _monitor && monitor != HMONITOR.Null)
        {
            _monitor = monitor;
            _signal(new WindowDragSignal(WindowDragStage.MonitorChanged));
        }
    }

    private void End(string reason)
    {
        if (_locationHook != HWINEVENTHOOK.Null)
        {
            PInvoke.UnhookWinEvent(_locationHook);
            _locationHook = HWINEVENTHOOK.Null;
        }

        if (_timer != 0)
        {
            PInvoke.KillTimer(HWND.Null, _timer);
            _timer = 0;
        }

        _window = HWND.Null;

        // A click without a change was never reported, so it has no end either.
        if (_started)
        {
            _started = false;
            var durationMs = (long)_time.GetElapsedTime(_startedAt).TotalMilliseconds;
            _log.DragEnded(reason, durationMs);
            _signal(new WindowDragSignal(WindowDragStage.Ended));
        }
    }

}
