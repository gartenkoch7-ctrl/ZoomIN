using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tracking;

/// <summary>
/// Rules that tell a moved caret from one that only blinked or was reported by another source.
/// Found with tools/ZoomIN.TrackingProbe: Notepad drops its system caret while blinking, so the
/// chain alternates between the system caret (caret height) and UI Automation (line height) at
/// the same spot. Counting that as movement would pull the view back to the caret every blink.
/// </summary>
public static class CaretMotion
{
    // Rounding between sources; a typed character moves the caret by several pixels.
    private const double SamePlacePixels = 1;

    /// <summary>
    /// Same position: same left edge and same bottom (sources disagree about the height – caret
    /// vs. line – but agree on the baseline).
    /// </summary>
    public static bool SamePlace(PhysicalRect a, PhysicalRect b) =>
        Math.Abs(a.X - b.X) <= SamePlacePixels && Math.Abs(a.Bottom - b.Bottom) <= SamePlacePixels;

    /// <summary>
    /// Wider than high: not a text cursor but a cell (Excel reports its active cell as the system
    /// caret). Followed like a caret, but not drawn as a bar. Block carets (console) are narrower.
    /// </summary>
    public static bool IsCell(PhysicalRect caret) => caret.Width > caret.Height;
}
