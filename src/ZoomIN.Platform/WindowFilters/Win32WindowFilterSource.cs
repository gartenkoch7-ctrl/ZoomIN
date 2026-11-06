using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Platform.WindowFilters;

/// <summary>
/// Thread of the filter per window (ADR-0020). Out-of-context WinEvents (show, hide, move,
/// Z order, foreground, cloak) arrive as messages on this thread; each burst ends in one update,
/// because the update request is posted behind the events already queued. A timer hands the due
/// looks at new windows to the <see cref="LookWorker"/> and runs only while one is due.
/// </summary>
public sealed class Win32WindowFilterSource : IWindowFilterSource
{
    private const uint UpdateMessage = PInvoke.WM_APP + 1;
    private const uint LooksDoneMessage = PInvoke.WM_APP + 2;
    private const uint LookInterval = 50;

    // Event ranges: foreground; move/size; minimize; destroy–reorder; location; cloak/uncloak.
    private static readonly (uint Min, uint Max)[] s_events =
        [(0x0003, 0x0003), (0x000A, 0x000B), (0x0016, 0x0017), (0x8001, 0x8004), (0x800B, 0x800B), (0x8017, 0x8018)];

    private const uint EventForeground = 0x0003;
    private const uint EventDestroy = 0x8001;

    // One tracker per process; the static callback finds it here.
    private static Win32WindowFilterSource? s_current;
    private static readonly WINEVENTPROC s_callback = OnEvent;

    private readonly ILogger _log;
    private readonly IMonitorProvider _monitors;
    private readonly ManualResetEventSlim _ready = new();
    private readonly List<HWINEVENTHOOK> _hooks = [];
    private Thread? _thread;
    private WindowFilterModel? _model;
    private WindowTracker? _tracker;
    private uint _threadId;
    private readonly List<LookRequest> _due = [];
    private nuint _timer;
    private bool _updatePosted;

    public Win32WindowFilterSource(ILogger<Win32WindowFilterSource> log, IMonitorProvider monitors)
    {
        _log = log;
        _monitors = monitors;
    }

    public void Start(WindowFilterModel model)
    {
        if (_thread is not null)
        {
            return;
        }

        _model = model;
        model.OptionsChanged += RequestUpdate;
        _thread = new Thread(Run) { IsBackground = true, Name = "ZoomIN Window Filter" };
        _thread.Start();
        _ready.Wait(TimeSpan.FromSeconds(2));
    }

    public bool TryGetForeground(out WindowFilterTarget window)
    {
        window = default;
        return _tracker is { } tracker && tracker.Snapshot.TryFind(PInvoke.GetForegroundWindow(), out window);
    }

    public IReadOnlyList<WindowKind> OpenKinds() => _tracker?.Snapshot.Kinds() ?? [];

    public void Dispose()
    {
        if (_model is not null)
        {
            _model.OptionsChanged -= RequestUpdate;
        }

        if (_thread is not null)
        {
            PInvoke.PostThreadMessage(_threadId, PInvoke.WM_QUIT, default, default);
            _thread.Join(TimeSpan.FromSeconds(2));
        }

        _ready.Dispose();
    }

    private void Run()
    {
        _threadId = PInvoke.GetCurrentThreadId();
        PInvoke.PeekMessage(out _, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_NOREMOVE);
        // Looks run on their own thread, so this one only lists windows and never waits for DWM.
        using var tracker = new WindowTracker(_model!, _monitors, new WindowFilterTelemetry(_log), null);
        var threadId = _threadId;
        using var looks = new LookWorker(_log, () => PInvoke.PostThreadMessage(threadId, LooksDoneMessage, default, default));
        _tracker = tracker;
        s_current = this;
        Hook();
        _ready.Set();
        Safely(tracker.Update);
        UpdateTimer();

        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0) > 0)
        {
            if (msg.message == UpdateMessage)
            {
                _updatePosted = false;
                Safely(tracker.Update);
                UpdateTimer();
            }
            else if (msg.message == LooksDoneMessage)
            {
                Safely(() => Decide(tracker, looks.TakeResults()));
                UpdateTimer();
            }
            else if (msg.message == PInvoke.WM_TIMER && msg.wParam.Value == _timer)
            {
                Safely(() =>
                {
                    if (!looks.Busy && tracker.CollectDueLooks(_due))
                    {
                        looks.TryStart(_due);
                    }

                    // Deadlines pass without results too (window gone or covered).
                    Decide(tracker, []);
                });
                UpdateTimer();
            }
            else
            {
                // Delivers the WinEvent callbacks.
                PInvoke.DispatchMessage(msg);
            }
        }

        foreach (var hook in _hooks)
        {
            PInvoke.UnhookWinEvent(hook);
        }

        s_current = null;
    }

    private void Hook()
    {
        foreach (var (min, max) in s_events)
        {
            var hook = PInvoke.SetWinEventHook(min, max, HMODULE.Null, s_callback, 0, 0, 0); // WINEVENT_OUTOFCONTEXT
            if (hook != HWINEVENTHOOK.Null)
            {
                _hooks.Add(hook);
            }
        }

        if (_hooks.Count == 0)
        {
            _log.WindowHooksFailed();
        }
        else
        {
            _log.WindowFilterStarted(_hooks.Count);
        }
    }

    private static void Decide(WindowTracker tracker, ReadOnlySpan<LookResult> results)
    {
        if (tracker.Apply(results))
        {
            tracker.Update();
        }
    }

    private static void OnEvent(HWINEVENTHOOK hook, uint @event, HWND hwnd, int idObject, int idChild, uint thread, uint time)
    {
        // Only whole top-level windows; carets and controls report location changes too. A
        // destroyed window has no ancestor any more, so destroy events skip that check.
        if (s_current is not { } source || hwnd == HWND.Null || idObject != 0 || idChild != 0
            || (@event != EventDestroy && PInvoke.GetAncestor(hwnd, GET_ANCESTOR_FLAGS.GA_ROOT) != hwnd))
        {
            return;
        }

        if (@event == EventForeground)
        {
            source._tracker?.ForegroundChanged(hwnd);
        }

        source.RequestUpdate();
    }

    // From the hook (tracker thread) or the settings (any thread); a posted flag keeps one request in the queue.
    private void RequestUpdate()
    {
        if (Thread.CurrentThread == _thread)
        {
            if (_updatePosted)
            {
                return;
            }

            _updatePosted = true;
        }

        PInvoke.PostThreadMessage(_threadId, UpdateMessage, default, default);
    }

    private void UpdateTimer()
    {
        var needed = _tracker?.HasPendingLooks == true;
        if (needed && _timer == 0)
        {
            _timer = PInvoke.SetTimer(HWND.Null, 0, LookInterval, null);
        }
        else if (!needed && _timer != 0)
        {
            PInvoke.KillTimer(HWND.Null, _timer);
            _timer = 0;
        }
    }

    private void Safely(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A failing update must not end the thread; the next event tries again.
            _log.WindowUpdateFailed(ex);
        }
    }
}
