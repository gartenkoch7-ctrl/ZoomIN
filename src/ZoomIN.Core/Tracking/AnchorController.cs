using System.Diagnostics;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tracking;

/// <summary>
/// Decides per monitor and frame which point the view is anchored to: mouse, keyboard focus,
/// text caret (M6) or a Windows notification. Moves only when the target is not visible yet,
/// glides there, and hands the view back to the mouse once it moves (or the notification closes).
/// Owned by one render thread; no allocations.
/// </summary>
public sealed class AnchorController
{
    // The mouse must move this far after a focus/caret jump to take the view back.
    private const double MouseReturnPixels = 8;

    // Focus changes this soon after a mouse movement were caused by the mouse (e.g. a click).
    private const double MouseIntentSeconds = 0.5;

    // Focus rectangles covering more of the monitor (whole windows/documents) say nothing useful.
    private const double MaxFocusShare = 0.5;

    private const double SettleDistance = 0.5;

    private bool _initialized;
    private PhysicalPoint _anchor;
    private PhysicalPoint _target;
    private bool _gliding;
    private long _lastTick;
    private PhysicalPoint _lastMouse;
    private long _lastMouseMoveAt;
    private PhysicalPoint _mouseAtSwitch;
    private long _seenFocusAt;
    private long _seenCaretAt;
    private long _seenNotificationAt;
    private long _seenRequestAt;

    public AnchorSource Source { get; private set; } = AnchorSource.Mouse;

    public PhysicalPoint Next(in AnchorInput input)
    {
        if (!_initialized)
        {
            Initialize(input);
        }

        // Capped like the zoom animation: after a pause (renderer asleep) the first frame must
        // start a glide, not jump to its end.
        var delta = Math.Min(Seconds(input.Now - _lastTick), 0.1);
        _lastTick = input.Now;

        TrackMouse(input);
        if (input.Options.Enabled)
        {
            TrackKeyboard(input);
        }

        // After the keyboard: a toast that just appeared wins over a focus change in the same frame.
        TrackNotification(input);
        Move(input, delta);
        return _anchor;
    }

    private void Initialize(in AnchorInput input)
    {
        _anchor = input.MouseInside ? input.Mouse : Center(input.Monitor);
        _lastMouse = input.Mouse;
        _lastTick = input.Now;

        // Focus and caret from before this monitor started rendering are not news.
        _seenFocusAt = input.Tracking.FocusChangedAt;
        _seenCaretAt = input.Tracking.CaretChangedAt;
        _seenNotificationAt = input.Tracking.NotificationChangedAt;
        _seenRequestAt = input.NotificationRequestedAt;
        _initialized = true;
    }

    private void TrackMouse(in AnchorInput input)
    {
        if (input.Mouse != _lastMouse)
        {
            _lastMouse = input.Mouse;
            _lastMouseMoveAt = input.Now;
        }

        if (Source != AnchorSource.Mouse && input.MouseInside && Distance(input.Mouse, _mouseAtSwitch) > MouseReturnPixels)
        {
            Source = AnchorSource.Mouse;
            _gliding = true;
        }
    }

    private void TrackKeyboard(in AnchorInput input)
    {
        var tracking = input.Tracking;
        var mouseActive = MouseActive(input);
        var caretNews = tracking.CaretChangedAt > _seenCaretAt;
        var focusNews = tracking.FocusChangedAt > _seenFocusAt;
        _seenCaretAt = tracking.CaretChangedAt;
        _seenFocusAt = tracking.FocusChangedAt;
        if (mouseActive)
        {
            return;
        }

        // The caret wins over the focus: while typing, the caret is where the user looks.
        if (caretNews && input.Options.Caret && tracking.Caret is { } caret && input.Monitor.Contains(Center(caret)))
        {
            Follow(AnchorSource.Caret, caret, input.Options.CaretMargin, input);
        }
        else if (focusNews && input.Options.Focus && tracking.Focus is { } focus && Usable(focus, input.Monitor))
        {
            Follow(AnchorSource.Focus, focus, 0, input);
        }
    }

    private void TrackNotification(in AnchorInput input)
    {
        var toast = input.Tracking.Notification;
        if (Source == AnchorSource.Notification && toast is null)
        {
            // The toast closed: back to the mouse (user decision: stay only while it is visible).
            Source = AnchorSource.Mouse;
            _gliding = true;
        }

        var requested = input.NotificationRequestedAt > _seenRequestAt;
        _seenRequestAt = input.NotificationRequestedAt;
        var news = input.Options.Notifications && input.Tracking.NotificationChangedAt > _seenNotificationAt;
        if (!requested && (!news || MouseActive(input)))
        {
            // While the mouse is moving the news stays unread: the view pans once the mouse rests.
            return;
        }

        _seenNotificationAt = input.Tracking.NotificationChangedAt;
        if (toast is { } rect && input.Monitor.Contains(Center(rect)))
        {
            Follow(AnchorSource.Notification, rect, 0, input);
        }
    }

    private void Follow(AnchorSource source, PhysicalRect rect, double margin, in AnchorInput input)
    {
        Source = source;
        _mouseAtSwitch = _lastMouse;

        // Where the view is heading, not where it is in the middle of a glide.
        var heading = _gliding ? _target : _anchor;
        var visible = ModeGeometry.Compute(input.Mode, input.Monitor, heading, input.Zoom, input.Lens, input.Dock).Source;
        if (Inside(rect, Shrink(visible, margin)))
        {
            return;
        }

        _target = Center(rect);
        _gliding = true;
    }

    private void Move(in AnchorInput input, double delta)
    {
        if (Source == AnchorSource.Mouse)
        {
            if (!input.MouseInside)
            {
                // The mouse is on another monitor: this one keeps its last view (M3 decision).
                _gliding = false;
                return;
            }

            _target = input.Mouse;
            if (!_gliding)
            {
                // Exactly at the mouse: the fixpoint that makes clicks hit (ADR-0003).
                _anchor = input.Mouse;
                return;
            }
        }

        if (!_gliding)
        {
            return;
        }

        var pan = input.Options.PanSeconds;
        if (pan > 0 && Distance(_anchor, _target) > SettleDistance)
        {
            var alpha = 1 - Math.Exp(-3 * delta / pan);
            _anchor = new PhysicalPoint(_anchor.X + ((_target.X - _anchor.X) * alpha), _anchor.Y + ((_target.Y - _anchor.Y) * alpha));
            return;
        }

        _anchor = _target;
        _gliding = false;
    }

    private bool MouseActive(in AnchorInput input) =>
        _lastMouseMoveAt != 0 && Seconds(input.Now - _lastMouseMoveAt) < MouseIntentSeconds;

    private static bool Usable(PhysicalRect focus, PhysicalRect monitor) =>
        monitor.Contains(Center(focus)) && focus.Width * focus.Height <= monitor.Width * monitor.Height * MaxFocusShare;

    private static bool Inside(PhysicalRect inner, PhysicalRect outer) =>
        inner.X >= outer.X && inner.Right <= outer.Right && inner.Y >= outer.Y && inner.Bottom <= outer.Bottom;

    private static PhysicalRect Shrink(PhysicalRect r, double margin) =>
        new(r.X + (r.Width * margin), r.Y + (r.Height * margin), r.Width * (1 - (2 * margin)), r.Height * (1 - (2 * margin)));

    private static PhysicalPoint Center(PhysicalRect r) => new(r.X + (r.Width / 2), r.Y + (r.Height / 2));

    private static double Distance(PhysicalPoint a, PhysicalPoint b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));

    private static double Seconds(long ticks) => ticks / (double)Stopwatch.Frequency;
}
