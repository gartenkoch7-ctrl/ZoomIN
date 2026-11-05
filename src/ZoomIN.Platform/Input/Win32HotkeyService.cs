using System.Collections.Concurrent;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Hotkeys;

namespace ZoomIN.Platform.Input;

/// <summary>
/// Global shortcuts via RegisterHotKey on a dedicated thread. Thread-bound hotkeys (no window)
/// post WM_HOTKEY to that thread's queue, so registration must also happen on that thread.
/// </summary>
public sealed class Win32HotkeyService : IHotkeyService
{
    private readonly ILogger _log;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _queueReady = new();
    private readonly ConcurrentQueue<Registration> _pending = new();
    private readonly ConcurrentQueue<TaskCompletionSource> _unregisterRequests = new();
    private readonly Dictionary<int, Action> _callbacks = [];
    private uint _threadId;
    private int _nextId = 1;

    public Win32HotkeyService(ILogger<Win32HotkeyService> log)
    {
        _log = log;
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
        _queueReady.Dispose();
    }

    private void Run()
    {
        _threadId = PInvoke.GetCurrentThreadId();

        // A thread only gets a message queue once it touches the message API.
        PInvoke.PeekMessage(out _, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_NOREMOVE);
        _queueReady.Set();

        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0) > 0)
        {
            if (msg.message == PInvoke.WM_HOTKEY && _callbacks.TryGetValue((int)msg.wParam.Value, out var callback))
            {
                Invoke(callback);
            }
            else if (msg.message == PInvoke.WM_APP)
            {
                RegisterPending();
            }
            else if (msg.message == PInvoke.WM_APP + 1)
            {
                UnregisterRegistered();
                while (_unregisterRequests.TryDequeue(out var done))
                {
                    done.SetResult();
                }
            }
        }

        UnregisterRegistered();
    }

    private void UnregisterRegistered()
    {
        foreach (var id in _callbacks.Keys)
        {
            PInvoke.UnregisterHotKey(HWND.Null, id);
        }

        _callbacks.Clear();
    }

    private void RegisterPending()
    {
        while (_pending.TryDequeue(out var registration))
        {
            var id = _nextId++;
            var modifiers = (HOT_KEY_MODIFIERS)registration.Gesture.Modifiers;
            if (!registration.Repeat)
            {
                modifiers |= HOT_KEY_MODIFIERS.MOD_NOREPEAT;
            }

            var ok = PInvoke.RegisterHotKey(HWND.Null, id, modifiers, (uint)registration.Gesture.VirtualKey);
            if (ok)
            {
                _callbacks[id] = registration.OnPressed;
            }
            else
            {
                _log.HotkeyInUse(registration.Gesture);
            }

            registration.Result.SetResult(ok);
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
