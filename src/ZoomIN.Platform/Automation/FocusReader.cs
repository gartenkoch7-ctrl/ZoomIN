using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using ZoomIN.Core.Tracking;
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

    /// <summary>Where the caret of the last <see cref="Read"/> came from (telemetry).</summary>
    public CaretSource LastSource { get; private set; }

    /// <summary>Process owning the focus window at the last <see cref="Read"/>; 0 = none.</summary>
    public uint LastProcessId { get; private set; }

    public FocusReading Read()
    {
        var caret = GuiThreadCaret.Read(out var window);
        var source = caret is null ? CaretSource.None : CaretSource.System;
        LastProcessId = ProcessOf(window);

        // Before asking: Chromium only answers once its accessibility has been switched on.
        _chromium.Touch(window);
        if (caret is null && MsaaCaret.Read(window) is { } msaa)
        {
            (caret, source) = (msaa, CaretSource.Msaa);
        }

        IUIAutomationElement? element = null;
        try
        {
            element = _automation.GetFocusedElement();
            var focus = Bounds(element);
            if (caret is null && UiaTextCaret.Read(element) is { } textCaret)
            {
                (caret, source) = (textCaret, CaretSource.Uia);
            }

            if (caret is null && focus is { } f && CanStandInForCaret(element, f))
            {
                LastSource = CaretSource.Estimated;
                return new FocusReading(focus, f, CaretEstimated: true);
            }

            LastSource = IsCell(caret) ? CaretSource.Cell : source;
            return new FocusReading(focus, caret, IsCell(caret));
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            // Focus in a window that does not answer (or just closed): only the caret this time.
            LastSource = IsCell(caret) ? CaretSource.Cell : source;
            return new FocusReading(null, caret, IsCell(caret));
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

    private static unsafe uint ProcessOf(HWND window)
    {
        if (window.IsNull)
        {
            return 0;
        }

        uint processId;

        // 0 (thread id) means the window is gone; then processId is 0 as well.
        return PInvoke.GetWindowThreadProcessId(window, &processId) == 0 ? 0 : processId;
    }

    // Excel reports its active cell as the system caret: follow it, but do not draw it as a bar.
    private static bool IsCell(PhysicalRect? caret) => caret is { } c && CaretMotion.IsCell(c);

    private static bool CanStandInForCaret(IUIAutomationElement element, PhysicalRect focus) =>
        focus.Width <= MaxEstimateWidth && focus.Height <= MaxEstimateHeight
        && element.CurrentControlType is UIA_CONTROLTYPE_ID.UIA_DataItemControlTypeId or UIA_CONTROLTYPE_ID.UIA_EditControlTypeId;
}
