using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.WindowDrag;

namespace ZoomIN.Platform.WindowDrag;

/// <summary>
/// Thread of the window drag source (G1). Own thread and hooks instead of the filter per window's
/// (<c>WindowEventHooks</c>): that thread lists windows after every event burst, and its hooks
/// belong to that feature; a drag must be noticed at once and only needs two event ranges.
/// The out-of-context WinEvents arrive as messages here.
/// </summary>
public sealed class Win32WindowDragSource : IWindowDragSource
{
    private readonly ILogger _log;
    private readonly TimeProvider _time;
    private readonly ManualResetEventSlim _ready = new();
    private Thread? _thread;
    private uint _threadId;
    private bool _stopping;

    public Win32WindowDragSource(ILogger<Win32WindowDragSource> log)
        : this(log, TimeProvider.System)
    {
    }

    internal Win32WindowDragSource(ILogger<Win32WindowDragSource> log, TimeProvider time)
    {
        _log = log;
        _time = time;
    }

    public void Start(Action<WindowDragSignal> signal, Func<bool> active)
    {
        ArgumentNullException.ThrowIfNull(signal);
        ArgumentNullException.ThrowIfNull(active);
        if (_thread is not null)
        {
            return;
        }

        _thread = new Thread(() => Run(signal, active)) { IsBackground = true, Name = "ZoomIN Window Drag" };
        _thread.Start(); // no wait: nothing needs the hooks right away, Dispose waits for them
    }

    public void Dispose()
    {
        var thread = Interlocked.Exchange(ref _thread, null);
        Volatile.Write(ref _stopping, true);
        if (thread is null)
        {
            _ready.Dispose();
            return;
        }

        // The thread id is known once the thread got ready (set before _ready, a full barrier).
        // A thread that ended already (its loop failed) gets no WM_QUIT: its id may be reused.
        if (!_ready.Wait(TimeSpan.FromSeconds(2)))
        {
            // Still starting: it sees _stopping once ready and ends by itself; _ready stays alive for it.
            return;
        }

        if (thread.IsAlive)
        {
            PInvoke.PostThreadMessage(_threadId, PInvoke.WM_QUIT, default, default);
            thread.Join(TimeSpan.FromSeconds(2));
        }

        _ready.Dispose();
    }

    private void Run(Action<WindowDragSignal> signal, Func<bool> active)
    {
        try
        {
            Loop(signal, active);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A background thread's exception would end ZoomIN; without this thread only G1 is off.
            _log.EventFailed(ex);
            try
            {
                _ready.Set();
            }
            catch (ObjectDisposedException)
            {
                // Disposed meanwhile: nobody waits any more.
            }
        }
    }

    private void Loop(Action<WindowDragSignal> signal, Func<bool> active)
    {
        _threadId = PInvoke.GetCurrentThreadId();
        PInvoke.PeekMessage(out _, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_NOREMOVE); // message queue before the hooks
        using var watcher = new WindowDragWatcher(signal, active, _log, _time);
        if (watcher.IsHooked)
        {
            _log.SourceStarted();
        }
        else
        {
            _log.HooksFailed();
        }

        _ready.Set();
        if (Volatile.Read(ref _stopping))
        {
            return; // disposed while starting, without a WM_QUIT
        }

        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0) > 0)
        {
            if (msg.message == PInvoke.WM_TIMER && msg.hwnd == HWND.Null)
            {
                Safely(watcher, msg.wParam.Value);
            }
            else
            {
                // Delivers the WinEvent callbacks.
                PInvoke.DispatchMessage(msg);
            }
        }
    }

    private void Safely(WindowDragWatcher watcher, nuint timer)
    {
        try
        {
            watcher.OnTimer(timer);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A failing check must not end the thread; the next event tries again.
            _log.EventFailed(ex);
        }
    }
}
