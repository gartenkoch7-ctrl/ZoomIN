using System.Runtime.InteropServices;
using Windows.Win32.UI.Accessibility;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.Automation;

/// <summary>
/// One reading of keyboard focus and text caret. The caret comes from the first source that knows
/// it, cheapest first: Win32 system caret → MSAA caret object (also Chromium, Firefox) → UI
/// Automation text (Word, terminals, WPF/WinUI). With none of them, a focused grid cell or edit
/// stands in for the caret (estimated), so e.g. arrowing through Excel scrolls like typing.
/// </summary>
internal sealed class FocusReader : IDisposable
{
    // Larger elements (whole documents, panes) say nothing about where the user types.
    private const double MaxEstimateWidth = 1600;
    private const double MaxEstimateHeight = 200;

    private readonly IUIAutomation _automation;
    private readonly ChromiumAccessibility _chromium = new();

    public FocusReader(IUIAutomation automation)
    {
        _automation = automation;
    }

    public FocusReading Read()
    {
        var caret = GuiThreadCaret.Read(out var window);

        // Before asking: Chromium only answers once its accessibility has been switched on.
        _chromium.Touch(window);
        caret ??= MsaaCaret.Read(window);

        IUIAutomationElement? element = null;
        try
        {
            element = _automation.GetFocusedElement();
            var focus = Bounds(element);
            if (caret is null && UiaTextCaret.Read(element) is { } textCaret)
            {
                caret = textCaret;
            }

            return caret is null && focus is { } f && CanStandInForCaret(element, f)
                ? new FocusReading(focus, f, CaretEstimated: true)
                : new FocusReading(focus, caret, CaretEstimated: false);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            // Focus in a window that does not answer (or just closed): only the caret this time.
            return new FocusReading(null, caret, CaretEstimated: false);
        }
        finally
        {
            if (element is not null)
            {
                Marshal.ReleaseComObject(element);
            }
        }
    }

    public void Dispose() => Marshal.ReleaseComObject(_automation);

    private static PhysicalRect? Bounds(IUIAutomationElement element)
    {
        var r = element.CurrentBoundingRectangle;
        return r.right > r.left && r.bottom > r.top
            ? new PhysicalRect(r.left, r.top, r.right - r.left, r.bottom - r.top)
            : null;
    }

    private static bool CanStandInForCaret(IUIAutomationElement element, PhysicalRect focus) =>
        focus.Width <= MaxEstimateWidth && focus.Height <= MaxEstimateHeight
        && element.CurrentControlType is UIA_CONTROLTYPE_ID.UIA_DataItemControlTypeId or UIA_CONTROLTYPE_ID.UIA_EditControlTypeId;
}
