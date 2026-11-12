using System.Diagnostics;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Viewport;
using static ZoomIN.Core.Tracking.AnchorGeometry;

namespace ZoomIN.Core.Tracking;

/// <summary>
/// Decides per monitor and frame which point the view is anchored to: mouse, keyboard focus or
/// text caret (M6). A new focus glides to the middle of the view; the caret pushes the view once
/// it reaches the margin. Hands the view back to the mouse once it moves. Owned by one render
/// thread; no allocations. How the view follows the mouse: <see cref="MouseFollowTarget"/> (M17 A1).
/// A <see cref="ViewPlacement"/> puts the mouse view at a requested point (M17 B2).
/// </summary>
public sealed class AnchorController
{
    // The mouse must move this far after a focus/caret jump to take the view back.
    private const double MouseReturnPixels = 8;

    // Focus changes this soon after a mouse movement were caused by the mouse (e.g. a click).
    private const double MouseIntentSeconds = 0.5;

    private const double SettleDistance = 0.5;

    private bool _initialized;
    private PhysicalPoint? _startAt;
    private PhysicalPoint _anchor;
    private PhysicalPoint _target;
    private readonly PlacementTracker _placement = new();
    private double _lastZoom;
    private ZoomMode _lastMode;

    // Desktop point the view centres on while following focus/caret. Kept instead of a fixpoint
    // because the fixpoint that centres it depends on the zoom, which may still be animating.
    private PhysicalPoint _center;
    private bool _gliding;
    private bool _wasHeld;
    private long _lastTick;
    private PhysicalPoint _lastMouse;
    private long _lastMouseMoveAt;
    private PhysicalPoint _mouseAtSwitch;
    private long _seenFocusAt;
    private long _seenCaretAt;

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
        Place(input);
        if (input.Options.Enabled)
        {
            TrackKeyboard(input);
        }

        Move(input, delta);
        _lastZoom = input.Zoom;

        // A mode change while the mouse is on another monitor, or while a placed view holds, is
        // news once that is over.
        if ((input.MouseInside || Source != AnchorSource.Mouse) && !_wasHeld)
        {
            _lastMode = input.Mode;
        }

        return _anchor;
    }

    /// <summary>
    /// Where the view starts if this controller has not run yet and the mouse is elsewhere, instead of
    /// the monitor's middle: a pipeline rebuilt while its monitor was frozen (G2) goes on from the frozen view.
    /// </summary>
    public void StartAt(PhysicalPoint anchor) => _startAt = anchor;

    private void Initialize(in AnchorInput input)
    {
        _anchor = input.MouseInside ? input.Mouse : _startAt ?? Center(input.Monitor);
        _lastMouse = input.Mouse;
        _lastTick = input.Now;
        _lastZoom = input.Zoom;
        _lastMode = input.Mode;

        // Focus, caret and placements from before this monitor started rendering are not news.
        _seenFocusAt = input.Tracking.FocusChangedAt;
        _seenCaretAt = input.Tracking.CaretChangedAt;
        _placement.Initialize(input.Placement.At);
        _initialized = true;
    }

    private void Place(in AnchorInput input)
    {
        // Only the margin keeps a placed view; elsewhere the mouse decides at once (ViewPlacement).
        if (_placement.TryTake(input, out var anchor) && PlacementTracker.Applies(input))
        {
            // The mouse view starts there; with the margin it stays until the pointer pushes it.
            Switch(AnchorSource.Mouse);
            _anchor = _target = anchor;
            _gliding = false;
        }
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
            AnchorMetrics.Switched(AnchorSource.Mouse);
            _gliding = true;
        }
    }

    private void TrackKeyboard(in AnchorInput input)
    {
        var tracking = input.Tracking;
        var mouseActive = _lastMouseMoveAt != 0 && Seconds(input.Now - _lastMouseMoveAt) < MouseIntentSeconds;
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
            FollowCaret(caret, input);
        }
        else if (focusNews && input.Options.Focus && tracking.Focus is { } focus && Usable(focus, input.Monitor))
        {
            // Every new focus goes to the middle, even if it was already visible near the edge.
            Switch(AnchorSource.Focus);
            _center = Center(focus);
            _gliding = true;
        }
    }

    private void FollowCaret(PhysicalRect caret, in AnchorInput input)
    {
        Switch(AnchorSource.Caret);

        // Where the view is heading, not where it is in the middle of a glide.
        var heading = _gliding ? _target : _anchor;
        var visible = ModeGeometry.Compute(input.Mode, input.Monitor, heading, input.Zoom, input.Lens, input.Dock).Source;
        var inner = Shrink(visible, input.Options.CaretMargin);
        if (Inside(caret, inner))
        {
            if (!_gliding)
            {
                // Stay exactly where the view is (the mouse may have left it off-centre).
                _center = Center(visible);
            }

            return;
        }

        _center = CaretCenter(caret, visible, inner);
        _gliding = true;
    }

    private void Switch(AnchorSource source)
    {
        // Remember where the mouse was when the keyboard took over, not at every caret step.
        if (Source == AnchorSource.Mouse)
        {
            _mouseAtSwitch = _lastMouse;
        }

        if (Source != source)
        {
            AnchorMetrics.Switched(source);
        }

        Source = source;
    }

    private void Move(in AnchorInput input, double delta)
    {
        // A placed view only holds while the mouse drives this monitor.
        _wasHeld &= Source == AnchorSource.Mouse && input.MouseInside;
        if (Source == AnchorSource.Mouse)
        {
            if (!input.MouseInside)
            {
                // The mouse is on another monitor: this one keeps its last view (M3 decision).
                _gliding = false;
                return;
            }

            // Back from focus/caret the margin starts from where the view is heading, so nothing
            // jumps; a zoom change during that glide re-anchors nothing (the pointer moves anyway).
            // With the margin, a placed view keeps its anchor until the zoom has glided there.
            // A new mode (e.g. the lens) opens at the pointer, as the old anchor means another view.
            // After a placed view the margin may have to bring the pointer back: glide, not jump.
            var held = _placement.HeldThisFrame && PlacementTracker.Applies(input);
            var heading = input.Mode != _lastMode && !held ? input.Mouse : _gliding ? _target : _anchor;
            var previousZoom = _gliding ? input.Zoom : _lastZoom;
            _gliding |= _wasHeld && !held;
            _wasHeld = held;
            var target = held
                ? heading
                : MouseFollowTarget.For(input, heading, previousZoom);
            AnchorMetrics.MouseStep(input, _lastMouseMoveAt == input.Now && target != _target, _lastZoom);
            _target = target;
        }
        else
        {
            // Recomputed every frame: a zoom change must keep focus/caret where it is in the view.
            _target = ViewCentering.AnchorFor(input.Mode, input.Monitor, _center, input.Zoom, input.Lens, input.Dock);
        }

        if (!_gliding)
        {
            _anchor = _target;
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

    private static double Seconds(long ticks) => ticks / (double)Stopwatch.Frequency;
}
