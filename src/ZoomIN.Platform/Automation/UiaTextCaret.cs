using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Accessibility;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.Automation;

/// <summary>
/// The caret of the focused element as UI Automation text reports it. Covers applications that
/// draw their own caret but implement the text pattern: Word, Windows Terminal, the console,
/// WPF/WinUI and Chromium. TextPattern2 knows the caret directly; plain TextPattern only knows the
/// selection, whose end is the best guess for the caret.
/// </summary>
internal static unsafe class UiaTextCaret
{
    public static PhysicalRect? Read(IUIAutomationElement element)
    {
        var range = CaretRange(element);
        if (range is null)
        {
            return null;
        }

        try
        {
            return Bounds(range);
        }
        finally
        {
            Marshal.ReleaseComObject(range);
        }
    }

    private static IUIAutomationTextRange? CaretRange(IUIAutomationElement element)
    {
        try
        {
            if (element.GetCurrentPattern(UIA_PATTERN_ID.UIA_TextPattern2Id) is IUIAutomationTextPattern2 pattern2)
            {
                try
                {
                    BOOL active;
                    var range = pattern2.GetCaretRange(&active);
                    if (active)
                    {
                        return range;
                    }

                    // The text does not have keyboard focus: its caret is not the one the user types in.
                    Marshal.ReleaseComObject(range);
                    return null;
                }
                finally
                {
                    Marshal.ReleaseComObject(pattern2);
                }
            }

            return element.GetCurrentPattern(UIA_PATTERN_ID.UIA_TextPatternId) is IUIAutomationTextPattern pattern
                ? LastSelection(pattern)
                : null;
        }
        catch (Exception ex) when (ComFailure.IsAnswer(ex))
        {
            return null;
        }
    }

    private static IUIAutomationTextRange? LastSelection(IUIAutomationTextPattern pattern)
    {
        IUIAutomationTextRangeArray? selection = null;
        try
        {
            selection = pattern.GetSelection();
            return selection.Length > 0 ? selection.GetElement(selection.Length - 1) : null;
        }
        finally
        {
            if (selection is not null)
            {
                Marshal.ReleaseComObject(selection);
            }

            Marshal.ReleaseComObject(pattern);
        }
    }

    private static PhysicalRect? Bounds(IUIAutomationTextRange range)
    {
        if (LastRectangle(range) is { } rect)
        {
            // A selection: the caret sits at its end. A degenerate range has width 0 anyway.
            return new PhysicalRect(rect.Right - 1, rect.Y, 1, rect.Height);
        }

        // Most providers report no rectangle for an empty range (the caret itself). The character
        // after it has one; the caret is its left edge.
        var character = range.Clone();
        try
        {
            character.ExpandToEnclosingUnit(TextUnit.TextUnit_Character);
            return LastRectangle(character) is { } c ? new PhysicalRect(c.X, c.Y, 1, c.Height) : null;
        }
        finally
        {
            Marshal.ReleaseComObject(character);
        }
    }

    /// <summary>The last of the range's rectangles (one per line), already in physical pixels.</summary>
    private static PhysicalRect? LastRectangle(IUIAutomationTextRange range)
    {
        var array = range.GetBoundingRectangles();
        if (array is null)
        {
            return null;
        }

        try
        {
            // Flat array of doubles: left, top, width, height per rectangle.
            if (PInvoke.SafeArrayGetLBound(array, 1, out var lower).Failed
                || PInvoke.SafeArrayGetUBound(array, 1, out var upper).Failed
                || upper - lower + 1 < 4)
            {
                return null;
            }

            var first = upper - 3;
            Span<double> values = stackalloc double[4];
            for (var i = 0; i < 4; i++)
            {
                var index = first + i;
                double value;
                if (PInvoke.SafeArrayGetElement(array, in index, &value).Failed)
                {
                    return null;
                }

                values[i] = value;
            }

            return values[2] <= 0 && values[3] <= 0
                ? null
                : new PhysicalRect(values[0], values[1], Math.Max(values[2], 1), Math.Max(values[3], 1));
        }
        finally
        {
            PInvoke.SafeArrayDestroy(array);
        }
    }
}
