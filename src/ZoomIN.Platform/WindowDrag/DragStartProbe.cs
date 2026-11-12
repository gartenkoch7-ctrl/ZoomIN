using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowDrag;

namespace ZoomIN.Platform.WindowDrag;

/// <summary>
/// Decides when a move/size loop is a real drag and what kind (G1 mini-spike): not at the button
/// press (a plain click on a title bar enters the same loop) but at the first change. Kind from
/// the hit test where the button went down, else from the window's first change.
/// </summary>
/// <remarks>
/// With "Show window contents while dragging" off, Windows only draws an outline and moves the
/// window once at the release. Then the pointer moving past the drag threshold counts as the
/// first change (polled by the timer), and the release's own move must not start a drag. The
/// kind then comes from the hit test alone.
/// </remarks>
internal sealed unsafe class DragStartProbe
{
    private HWND _window;
    private PhysicalRect? _startRect;
    private bool _startMaximized;
    private PhysicalRect? _normal;
    private PhysicalPoint? _baseline;
    private int _thresholdX;
    private int _thresholdY;

    /// <summary>Hit-test code at the start (for the log), or <see cref="DragHitTest.NoAnswer"/>.</summary>
    public int HitTest { get; private set; }

    /// <summary>Kind from the hit test; unknown if it was unclear.</summary>
    public WindowDragKind HitKind { get; private set; }

    /// <summary>Windows drags an outline only: the window moves at the release.</summary>
    public bool OutlineOnly { get; private set; }

    /// <summary>
    /// Reads everything at MOVESIZESTART. The event arrives a moment late (out of context); under
    /// heavy load the pointer may have left the border already, then the hit test is unclear and
    /// the first change decides (with outline dragging it may even name the wrong zone).
    /// </summary>
    public void Begin(nint window)
    {
        _window = (HWND)window;
        _startRect = Bounds();
        _startMaximized = PInvoke.IsZoomed(_window);
        _normal = NormalBounds();
        var pointer = Pointer();

        // Pressed mouse button: the drag threshold counts from here. Keyboard (Alt+Space → Move):
        // Windows puts the pointer on the title bar itself, so it counts from the first poll.
        // GetAsyncKeyState reads physical buttons: with swapped buttons the primary one is the right.
        var primary = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_SWAPBUTTON) != 0 ? VIRTUAL_KEY.VK_RBUTTON : VIRTUAL_KEY.VK_LBUTTON;
        _baseline = (PInvoke.GetAsyncKeyState((int)primary) & 0x8000) != 0 ? pointer : null;
        HitTest = pointer is { } at ? DragHitTest.At(window, at) : DragHitTest.NoAnswer;
        HitKind = DragHitTest.KindOf(HitTest);
        _thresholdX = Math.Max(1, PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXDRAG));
        _thresholdY = Math.Max(1, PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYDRAG));

        BOOL fullWindows = true;
        OutlineOnly = PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETDRAGFULLWINDOWS, 0, &fullWindows, 0) && !fullWindows;
    }

    /// <summary>True once the drag counts as started; then <paramref name="kind"/> tells move or resize.</summary>
    /// <param name="polled">From the timer during the loop, not from a location event.</param>
    public bool TryStart(bool polled, out WindowDragKind kind)
    {
        kind = HitKind;
        if (OutlineOnly)
        {
            // The window does not change until the release; only the pointer tells a drag.
            if (!polled || Pointer() is not { } pointer)
            {
                return false;
            }

            if (_baseline is not { } start)
            {
                _baseline = pointer;
                return false;
            }

            return Math.Abs(pointer.X - start.X) >= _thresholdX || Math.Abs(pointer.Y - start.Y) >= _thresholdY;
        }

        if (_startRect is not { } before || Bounds() is not { } after || before == after)
        {
            return false;
        }

        // A maximized window cannot be resized; its first move restores it to another size.
        if (kind == WindowDragKind.Unknown)
        {
            kind = _startMaximized ? WindowDragKind.Move : WindowDragKinds.FromFirstChange(before, after, _normal);
        }

        return true;
    }

    // The size Windows restores when a snapped window is moved (only the size is compared; the
    // position is in work-area coordinates).
    private PhysicalRect? NormalBounds()
    {
        var placement = new WINDOWPLACEMENT { length = (uint)sizeof(WINDOWPLACEMENT) };
        if (!PInvoke.GetWindowPlacement(_window, ref placement))
        {
            return null;
        }

        var r = placement.rcNormalPosition;
        return new PhysicalRect(r.left, r.top, r.right - r.left, r.bottom - r.top);
    }

    private PhysicalRect? Bounds() =>
        PInvoke.GetWindowRect(_window, out var r) ? new PhysicalRect(r.left, r.top, r.right - r.left, r.bottom - r.top) : null;

    private static PhysicalPoint? Pointer() => PInvoke.GetCursorPos(out var p) ? new PhysicalPoint(p.X, p.Y) : null;
}
