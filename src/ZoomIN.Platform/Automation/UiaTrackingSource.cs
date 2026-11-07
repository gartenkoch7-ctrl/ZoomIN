using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.Automation;

/// <summary>
/// Polls keyboard focus (UI Automation), the text caret (GetGUIThreadInfo) and visible toasts
/// (<see cref="ToastWindowFinder"/>) about 30 times per second on its own thread. Polling instead
/// of event callbacks needs no COM callback objects and a hanging application can only delay this
/// thread, never the zoom (M6).
/// </summary>
public sealed unsafe class UiaTrackingSource : ITrackingSource
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(33);

    private readonly ILog _log;
    private readonly ManualResetEventSlim _stop = new();
    private Thread? _thread;

    public UiaTrackingSource(ILog log)
    {
        _log = log;
    }

    public void Start(TrackingModel model)
    {
        if (_thread is not null)
        {
            return;
        }

        _thread = new Thread(() => Run(model)) { IsBackground = true, Name = "ZoomIN Focus Tracking" };
        _thread.SetApartmentState(ApartmentState.MTA);
        _thread.Start();
    }

    public void Dispose()
    {
        _stop.Set();
        _thread?.Join(TimeSpan.FromSeconds(2));
        _stop.Dispose();
    }

    private void Run(TrackingModel model)
    {
        var automation = CreateAutomation();
        var toasts = new ToastWindowFinder(_log);
        PhysicalRect? focus = null;
        PhysicalRect? caret = null;
        PhysicalRect? toast = null;
        long focusAt = 0;
        long caretAt = 0;
        long toastAt = 0;

        while (!_stop.Wait(Interval))
        {
            var newFocus = automation is null ? null : ReadFocus(automation);
            var newCaret = ReadCaret();
            var newToast = toasts.Find();
            if (newFocus == focus && newCaret == caret && newToast == toast)
            {
                continue;
            }

            var now = Stopwatch.GetTimestamp();
            focusAt = Stamp(newFocus, focus, focusAt, now);
            caretAt = Stamp(newCaret, caret, caretAt, now);
            toastAt = Stamp(newToast, toast, toastAt, now);
            (focus, caret, toast) = (newFocus, newCaret, newToast);
            model.Publish(new TrackingSnapshot(focus, focusAt, caret, caretAt, toast, toastAt));
        }

        if (automation is not null)
        {
            Marshal.ReleaseComObject(automation);
        }
    }

    // A change to a new rectangle is news; disappearing is not (the old timestamp stays).
    private static long Stamp(PhysicalRect? next, PhysicalRect? previous, long changedAt, long now) =>
        next != previous && next is not null ? now : changedAt;

    private IUIAutomation? CreateAutomation()
    {
        try
        {
            return (IUIAutomation)new CUIAutomation();
        }
        catch (COMException ex)
        {
            _log.Error("UI Automation is unavailable; focus tracking is off.", ex);
            return null;
        }
    }

    private static PhysicalRect? ReadFocus(IUIAutomation automation)
    {
        try
        {
            var element = automation.GetFocusedElement();
            try
            {
                var r = element.CurrentBoundingRectangle;
                return r.right > r.left && r.bottom > r.top
                    ? new PhysicalRect(r.left, r.top, r.right - r.left, r.bottom - r.top)
                    : null;
            }
            finally
            {
                Marshal.ReleaseComObject(element);
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            // Focus in a window that does not answer (or just closed): no information this time.
            return null;
        }
    }

    private static PhysicalRect? ReadCaret()
    {
        var info = new GUITHREADINFO { cbSize = (uint)sizeof(GUITHREADINFO) };
        if (!PInvoke.GetGUIThreadInfo(0, ref info) || info.hwndCaret.IsNull)
        {
            return null;
        }

        var r = info.rcCaret;
        var topLeft = new Point(r.left, r.top);
        if (!PInvoke.ClientToScreen(info.hwndCaret, ref topLeft))
        {
            return null;
        }

        // Some applications report a zero-width caret.
        return new PhysicalRect(topLeft.X, topLeft.Y, Math.Max(r.right - r.left, 1), Math.Max(r.bottom - r.top, 1));
    }
}
