using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Accessibility;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.Automation;

/// <summary>
/// Polls keyboard focus and text caret (see <see cref="FocusReader"/>) about 30 times per
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
        using var reader = CreateReader();
        var last = new FocusReading(null, null, CaretEstimated: false);
        PhysicalRect? seenCaret = null;
        long focusAt = 0;
        long caretAt = 0;

        while (!_stop.Wait(Interval))
        {
            var next = reader?.Read() ?? new FocusReading(null, GuiThreadCaret.Read(out _), CaretEstimated: false);

            // Same spot from another source (blinking system caret vs. line-high UIA range): keep
            // the shape that is shown, so the caret bar does not flicker (CaretMotion).
            if (next.Caret is { } c && last.Caret is { } shown && CaretMotion.SamePlace(c, shown))
            {
                next = next with { Caret = shown };
            }

            if (next == last)
            {
                continue;
            }

            var now = Stopwatch.GetTimestamp();
            if (next.Focus != last.Focus && next.Focus is not null)
            {
                focusAt = now;
            }

            // Only a caret that moved is news; one that reappears where it was (blinking) is not,
            // or it would pull the view back from the mouse every blink.
            if (next.Caret is { } moved && (seenCaret is not { } seen || !CaretMotion.SamePlace(moved, seen)))
            {
                caretAt = now;
            }

            seenCaret = next.Caret ?? seenCaret;
            last = next;
            model.Publish(new TrackingSnapshot(next.Focus, focusAt, next.Caret, caretAt, next.CaretEstimated));
        }
    }

    private FocusReader? CreateReader()
    {
        try
        {
            return new FocusReader((IUIAutomation)new CUIAutomation());
        }
        catch (COMException ex)
        {
            _log.Error("UI Automation is unavailable; focus tracking is off.", ex);
            return null;
        }
    }
}
