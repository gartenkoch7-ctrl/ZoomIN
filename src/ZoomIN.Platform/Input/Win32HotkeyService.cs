using System.Collections.Concurrent;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Hotkeys;

namespace ZoomIN.Platform.Input;

/// <summary>
/// Global shortcuts via RegisterHotKey on a dedicated thread. Thread-bound hotkeys (no window)
/// post WM_HOTKEY to that thread's queue, so registration must also happen on that thread.
/// Shortcuts are given up while another desktop (Win+L, Ctrl+Alt+Del, UAC) has the input, so the
/// secure-desktop instance can have them there (<see cref="HotkeyTable"/>).
/// </summary>
public sealed class Win32HotkeyService : IHotkeyService
{
    // Kept in a static field so the GC never collects the delegate Windows calls back into; one
    // service per process (DI singleton), so the callback finds it through s_active.
    private static readonly WINEVENTPROC s_desktopSwitch = OnDesktopSwitch;
    private static Win32HotkeyService? s_active;

    private readonly ILogger _log;
    private readonly HotkeyTable _table;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _queueReady = new();
    private readonly ConcurrentQueue<Registration> _pending = new();
    private readonly ConcurrentQueue<TaskCompletionSource> _unregisterRequests = new();
    private uint _threadId;
    private nuint _retryTimer;

    public Win32HotkeyService(ILogger<Win32HotkeyService> log)
    {
        _log = log;
        _table = new HotkeyTable(log);
        Volatile.Write(ref s_active, this);
        _thread = new Thread(Run) { IsBackground = true, Name = "ZoomIN Hotkeys" };
        _thread.Start();
        _queueReady.Wait();
    }

    public bool Register(HotkeyGesture gesture, Action onPressed, bool repeat = false)
    {
        var registration = new Registration(gesture, onPressed, repeat);
        _pending.Enqueue(registration);
        PInvoke.PostThreadMessage(_threadId, PInvoke.WM_APP, default, default);
        return registration.Result.Task.GetAwaiter().GetResult();
    }

    public void UnregisterAll()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _unregisterRequests.Enqueue(done);
        PInvoke.PostThreadMessage(_threadId, PInvoke.WM_APP + 1, default, default);
        done.Task.Wait(TimeSpan.FromSeconds(2));
    }

    public void Dispose()
    {
        PInvoke.PostThreadMessage(_threadId, PInvoke.WM_QUIT, default, default);
        _thread.Join(TimeSpan.FromSeconds(2));
        Interlocked.CompareExchange(ref s_active, null, this);
        _queueReady.Dispose();
    }

    private void Run()
    {
        _threadId = PInvoke.GetCurrentThreadId();

        // A thread only gets a message queue once it touches the message API.
        PInvoke.PeekMessage(out _, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_NOREMOVE);

        // Out of context: Windows calls back on this thread while it waits in GetMessage. Not fatal
        // if missing: the shortcuts then simply stay registered, as before.
        var desktopHook = PInvoke.SetWinEventHook(
            PInvoke.EVENT_SYSTEM_DESKTOPSWITCH, PInvoke.EVENT_SYSTEM_DESKTOPSWITCH, HMODULE.Null, s_desktopSwitch, 0, 0, PInvoke.WINEVENT_OUTOFCONTEXT);
        _queueReady.Set();

        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0) > 0)
        {
            if (msg.message == PInvoke.WM_HOTKEY && _table.Callback((int)msg.wParam.Value) is { } callback)
            {
                Invoke(callback);
            }
            else if (msg.message == PInvoke.WM_APP)
            {
                RegisterPending();
            }
            else if (msg.message == PInvoke.WM_APP + 1)
            {
                _table.Clear();
                UpdateRetryTimer();
                while (_unregisterRequests.TryDequeue(out var done))
                {
                    done.SetResult();
                }
            }
            else if (msg.message == PInvoke.WM_TIMER && msg.wParam.Value == _retryTimer)
            {
                OnTimer();
            }
        }

        if (!desktopHook.IsNull)
        {
            PInvoke.UnhookWinEvent(desktopHook);
        }

        _table.Clear();
        UpdateRetryTimer();
    }

    private static void OnDesktopSwitch(HWINEVENTHOOK hook, uint winEvent, HWND hwnd, int idObject, int idChild, uint thread, uint time)
    {
        var self = Volatile.Read(ref s_active);
        if (self is null || PInvoke.GetCurrentThreadId() != self._threadId)
        {
            return;
        }

        try
        {
            self._table.DesktopChanged(InputDesktop.IsCurrent());
            self.UpdateRetryTimer();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Escaping a Windows callback would end ZoomIN; the shortcuts keep their state.
            self._log.HotkeyHandlerFailed(ex);
        }
    }

    private void OnTimer()
    {
        try
        {
            // While suspended, look again: a missed or undelivered switch event must not cost the shortcuts.
            if (_table.Suspended)
            {
                _table.DesktopChanged(InputDesktop.IsCurrent());
            }
            else
            {
                _table.Retry();
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.HotkeyHandlerFailed(ex);
        }

        UpdateRetryTimer();
    }

    private void RegisterPending()
    {
        while (_pending.TryDequeue(out var registration))
        {
            registration.Result.SetResult(_table.Add(registration.Gesture, registration.OnPressed, registration.Repeat));
        }

        UpdateRetryTimer();
    }

    private void UpdateRetryTimer()
    {
        if (_table.NeedsTimer && _retryTimer == 0)
        {
            _retryTimer = PInvoke.SetTimer(HWND.Null, 0, HotkeyTable.RetryIntervalMs, null);
        }
        else if (!_table.NeedsTimer && _retryTimer != 0)
        {
            PInvoke.KillTimer(HWND.Null, _retryTimer);
            _retryTimer = 0;
        }
    }

    private void Invoke(Action callback)
    {
        try
        {
            callback();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A failing handler must not kill the hotkey thread.
            _log.HotkeyHandlerFailed(ex);
        }
    }

    private sealed record Registration(HotkeyGesture Gesture, Action OnPressed, bool Repeat)
    {
        public TaskCompletionSource<bool> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
