using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ZoomIN.Platform.Input;

/// <summary>
/// A thread with a message loop for low-level hooks (WH_MOUSE_LL, WH_KEYBOARD_LL). Windows calls
/// such hooks on the thread that installed them, and only while it pumps messages; a hook that
/// keeps the system waiting is removed silently, so the loop does nothing else. The callers keep
/// their static callbacks, which only forward to their instance (ADR-0025).
/// </summary>
internal sealed class LowLevelHookThread : IDisposable
{
    private readonly Thread _thread;
    private uint _threadId;
    private bool _disposed;

    /// <summary>Starts the thread and returns once every hook was tried.</summary>
    public LowLevelHookThread(string name, params (WINDOWS_HOOK_ID Id, HOOKPROC Proc)[] hooks)
    {
        using var started = new ManualResetEventSlim();
        _thread = new Thread(() => Run(hooks, started)) { IsBackground = true, Name = name };
        _thread.Start();
        started.Wait();
    }

    /// <summary>False if Windows refused at least one hook; the others still run.</summary>
    public bool AllInstalled { get; private set; }

    public void Dispose()
    {
        // Once only: a later WM_QUIT would go to a thread id Windows may have given to another thread.
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        PInvoke.PostThreadMessage(_threadId, PInvoke.WM_QUIT, default, default);
        _thread.Join(TimeSpan.FromSeconds(2));
    }

    private void Run((WINDOWS_HOOK_ID Id, HOOKPROC Proc)[] hooks, ManualResetEventSlim started)
    {
        _threadId = PInvoke.GetCurrentThreadId();

        // Creates the message queue before the hooks: their messages and WM_QUIT need one.
        PInvoke.PeekMessage(out _, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_NOREMOVE);
        using var module = PInvoke.GetModuleHandle((string?)null);
        var handles = new SafeHandle[hooks.Length];
        try
        {
            var all = true;
            for (var i = 0; i < hooks.Length; i++)
            {
                handles[i] = PInvoke.SetWindowsHookEx(hooks[i].Id, hooks[i].Proc, module, 0);
                all &= !handles[i].IsInvalid;
            }

            AllInstalled = all;
            started.Set();
            while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0) > 0)
            {
                PInvoke.TranslateMessage(msg);
                PInvoke.DispatchMessage(msg);
            }
        }
        finally
        {
            foreach (var handle in handles)
            {
                handle?.Dispose();
            }
        }
    }
}
