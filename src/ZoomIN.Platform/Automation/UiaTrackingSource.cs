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
/// Polls keyboard focus (UI Automation) and the text caret (GetGUIThreadInfo) about 30 times per
/// second on its own thread. Polling instead of event callbacks needs no COM callback objects and
/// a hanging application can only delay this thread, never the zoom (M6).
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
        PhysicalRect? focus = null;
        PhysicalRect? caret = null;
        long focusAt = 0;
        long caretAt = 0;

        while (!_stop.Wait(Interval))
        {
            var newFocus = automation is null ? null : ReadFocus(automation);
            var newCaret = ReadCaret();
            if (newFocus == focus && newCaret == caret)
            {
                continue;
            }

            var now = Stopwatch.GetTimestamp();
            if (newFocus != focus && newFocus is not null)
            {
                focusAt = now;
            }

            if (newCaret != caret && newCaret is not null)
            {
                caretAt = now;
            }

            (focus, caret) = (newFocus, newCaret);
            model.Publish(new TrackingSnapshot(focus, focusAt, caret, caretAt));
        }

        if (automation is not null)
        {
            Marshal.ReleaseComObject(automation);
        }
    }

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

        // rcCaret is in the client coordinates of the caret window, i.e. in that window's DPI
        // space. Converted from a per-monitor-aware thread, a DPI-unaware or system-aware window
        // on a scaled monitor would put the caret off by the scale factor (too far right/down,
        // too small). So: client → screen in the window's own DPI context, then → physical.
        var r = info.rcCaret;
        var topLeft = new Point(r.left, r.top);
        var bottomRight = new Point(Math.Max(r.right, r.left + 1), Math.Max(r.bottom, r.top + 1));
        var previous = PInvoke.SetThreadDpiAwarenessContext(PInvoke.GetWindowDpiAwarenessContext(info.hwndCaret));
        try
        {
            if (!PInvoke.ClientToScreen(info.hwndCaret, ref topLeft) || !PInvoke.ClientToScreen(info.hwndCaret, ref bottomRight))
            {
                return null;
            }
        }
        finally
        {
            PInvoke.SetThreadDpiAwarenessContext(previous);
        }

        // Fails (and leaves the point unchanged) only if it lies outside the window; a caret
        // there is clipped anyway, so the unconverted point is the best guess.
        PInvoke.LogicalToPhysicalPointForPerMonitorDPI(info.hwndCaret, ref topLeft);
        PInvoke.LogicalToPhysicalPointForPerMonitorDPI(info.hwndCaret, ref bottomRight);

        // Some applications report a zero-width caret.
        return new PhysicalRect(topLeft.X, topLeft.Y, Math.Max(bottomRight.X - topLeft.X, 1), Math.Max(bottomRight.Y - topLeft.Y, 1));
    }
}
