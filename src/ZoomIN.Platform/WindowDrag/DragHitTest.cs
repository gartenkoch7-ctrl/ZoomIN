using System.Drawing;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowDrag;

namespace ZoomIN.Platform.WindowDrag;

/// <summary>
/// Asks a window what lies under a point (WM_NCHITTEST), the way Windows decided between move and
/// size when the drag began (G1 mini-spike). Works across processes; apps with their own title bar
/// answer it too, because Windows asks them the same.
/// </summary>
internal static unsafe class DragHitTest
{
    /// <summary>Result of <see cref="At"/> when the window did not answer.</summary>
    public const int NoAnswer = int.MinValue;

    /// <summary>Short: the app is inside its move loop and answers sent messages at once.</summary>
    private const uint TimeoutMs = 100;

    // WM_NCHITTEST results (winuser.h).
    private const int HitCaption = 2;
    private const int HitGrowBox = 4;
    private const int HitLeft = 10;
    private const int HitBottomRight = 17;

    /// <summary>
    /// Kind from the hit test at drag start. Title bar = move (also for a maximized window, whose
    /// first change is a new size); border or corner = resize. Anything else, e.g. the client area
    /// of apps that start the move themselves, is unknown.
    /// </summary>
    public static WindowDragKind KindOf(int hitTest) => hitTest switch
    {
        HitCaption => WindowDragKind.Move,
        HitGrowBox or (>= HitLeft and <= HitBottomRight) => WindowDragKind.Resize,
        _ => WindowDragKind.Unknown,
    };

    /// <summary>The hit-test code (e.g. 2 = title bar), or <see cref="NoAnswer"/>.</summary>
    public static int At(nint window, PhysicalPoint point)
    {
        var hwnd = (HWND)window;

        // The window expects the point in its own DPI space: a DPI-unaware window on a scaled
        // monitor would otherwise test a point off by the scale factor. Fails (and leaves the
        // point as it is) only outside the window; the physical point is the best guess then.
        var logical = new Point((int)point.X, (int)point.Y);
        PInvoke.PhysicalToLogicalPointForPerMonitorDPI(hwnd, ref logical);

        // SMTO_BLOCK: no WinEvent callbacks run inside this wait, so a drag never begins twice.
        // UIPI blocks the message to elevated windows: then it fails and the first change decides.
        nuint result;
        var lParam = (LPARAM)(nint)((logical.Y << 16) | (logical.X & 0xFFFF));
        var answered = PInvoke.SendMessageTimeout(
            hwnd, PInvoke.WM_NCHITTEST, default, lParam,
            SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_BLOCK | SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_ABORTIFHUNG, TimeoutMs, &result);
        return answered != 0 ? (int)(nint)result : NoAnswer;
    }
}
