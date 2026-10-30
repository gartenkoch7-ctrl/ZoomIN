using System.Collections.Concurrent;
using System.Globalization;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.Shell;

/// <summary>
/// Reserves the docked strip as a Windows AppBar. The shell talks to AppBars with messages, so
/// the AppBar window lives on its own thread with a message loop; calls are queued to it.
/// The window handle is written to <see cref="RecoveryFiles.AppBarWindow"/> so the watchdog can
/// give the space back after a crash.
/// </summary>
public sealed unsafe class AppBarReservation : IScreenReservation
{
    private const uint AbmNew = 0;
    private const uint AbmRemove = 1;
    private const uint AbmQueryPos = 2;
    private const uint AbmSetPos = 3;

    private readonly ILog _log;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private readonly BlockingCollection<Action> _work = [];
    private HWND _window;
    private bool _registered;

    public AppBarReservation(ILog log)
    {
        _log = log;
        _thread = new Thread(Run) { IsBackground = true, Name = "ZoomIN AppBar" };
        _thread.Start();
        _ready.Wait();
    }

    public void Reserve(PhysicalRect area, DockEdge edge) => Invoke(() => DoReserve(area, edge));

    public void Release() => Invoke(DoRelease);

    public void Dispose()
    {
        Release();
        _work.CompleteAdding();

        // Wake the message loop so it sees that no more work will come.
        PInvoke.PostMessage(_window, PInvoke.WM_APP, default, default);
        _thread.Join(TimeSpan.FromSeconds(2));
        _ready.Dispose();
    }

    private void Invoke(Action action)
    {
        using var done = new ManualResetEventSlim();
        _work.Add(() =>
        {
            action();
            done.Set();
        });
        PInvoke.PostMessage(_window, PInvoke.WM_APP, default, default);
        done.Wait(TimeSpan.FromSeconds(2));
    }

    private void Run()
    {
        // A STATIC window needs no own class; it is fully transparent and never takes input.
        _window = PInvoke.CreateWindowEx(
            WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE | WINDOW_EX_STYLE.WS_EX_LAYERED | WINDOW_EX_STYLE.WS_EX_TRANSPARENT,
            "STATIC", "ZoomIN Docked", WINDOW_STYLE.WS_POPUP, 0, 0, 0, 0, HWND.Null, null, null, null);
        PInvoke.SetLayeredWindowAttributes(_window, new COLORREF(0), 0, LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);
        _ready.Set();

        while (!_work.IsCompleted && PInvoke.GetMessage(out var msg, HWND.Null, 0, 0) > 0)
        {
            while (_work.TryTake(out var action))
            {
                action();
            }

            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(msg);
        }

        PInvoke.DestroyWindow(_window);
    }

    private void DoReserve(PhysicalRect area, DockEdge edge)
    {
        var data = Data();
        if (!_registered)
        {
            data.uCallbackMessage = PInvoke.WM_APP + 1;
            _registered = PInvoke.SHAppBarMessage(AbmNew, ref data) != 0;
        }

        data.uEdge = (uint)ToShellEdge(edge);
        data.rc = ToRect(area);

        // The shell may move the strip away from other AppBars (e.g. a top taskbar);
        // keep the wanted thickness from the edge it hands back.
        PInvoke.SHAppBarMessage(AbmQueryPos, ref data);
        KeepThickness(ref data.rc, edge, area);
        PInvoke.SHAppBarMessage(AbmSetPos, ref data);
        PInvoke.MoveWindow(_window, data.rc.left, data.rc.top, data.rc.right - data.rc.left, data.rc.bottom - data.rc.top, false);
        PInvoke.ShowWindow(_window, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
        WriteRecoveryFile();
        _log.Info($"Docked strip reserved: {edge} {data.rc.left},{data.rc.top}–{data.rc.right},{data.rc.bottom}");
    }

    private void DoRelease()
    {
        if (!_registered)
        {
            return;
        }

        var data = Data();
        PInvoke.SHAppBarMessage(AbmRemove, ref data);
        PInvoke.ShowWindow(_window, SHOW_WINDOW_CMD.SW_HIDE);
        _registered = false;
        File.Delete(RecoveryFiles.AppBarWindow);
        _log.Info("Docked strip released");
    }

    private APPBARDATA Data() => new() { cbSize = (uint)sizeof(APPBARDATA), hWnd = _window };

    private void WriteRecoveryFile()
    {
        Directory.CreateDirectory(RecoveryFiles.Directory);
        File.WriteAllText(RecoveryFiles.AppBarWindow, ((nint)_window.Value).ToString(CultureInfo.InvariantCulture));
    }

    private static void KeepThickness(ref RECT rc, DockEdge edge, PhysicalRect area)
    {
        switch (edge)
        {
            case DockEdge.Top: rc.bottom = rc.top + (int)area.Height; break;
            case DockEdge.Bottom: rc.top = rc.bottom - (int)area.Height; break;
            case DockEdge.Left: rc.right = rc.left + (int)area.Width; break;
            case DockEdge.Right: rc.left = rc.right - (int)area.Width; break;
        }
    }

    // ABE_LEFT = 0, ABE_TOP = 1, ABE_RIGHT = 2, ABE_BOTTOM = 3
    private static int ToShellEdge(DockEdge edge) => edge switch
    {
        DockEdge.Left => 0,
        DockEdge.Right => 2,
        DockEdge.Bottom => 3,
        _ => 1,
    };

    private static RECT ToRect(PhysicalRect r) => new()
    {
        left = (int)r.X,
        top = (int)r.Y,
        right = (int)r.Right,
        bottom = (int)r.Bottom,
    };
}
