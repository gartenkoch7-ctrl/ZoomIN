using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using ZoomIN.Platform.Automation;

namespace ZoomIN.Platform.Tests.Automation;

/// <summary>
/// Smoke tests for the caret sources: they answer for whatever window is in front without
/// throwing, and treat "no window" as "no caret". Which apps they cover is measured with
/// tools/ZoomIN.TrackingProbe (compatibility matrix in docs/kb/eingabe-und-tracking.md).
/// </summary>
public sealed class CaretSourcesTests
{
    [Fact]
    public void NoWindow_HasNoCaret_AndIsNotChromium()
    {
        Assert.Null(MsaaCaret.Read(HWND.Null));
        Assert.False(ChromiumAccessibility.IsChromium(HWND.Null));
        Assert.False(new ChromiumAccessibility().Touch(HWND.Null));
    }

    [Fact]
    public void ForegroundWindow_AllSources_DoNotThrow()
    {
        var exception = Record.Exception(() =>
        {
            GuiThreadCaret.Read(out var focus);
            MsaaCaret.Read(focus);
            new ChromiumAccessibility().Touch(focus);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void FocusReader_ReadsTheForegroundWindow_WithoutThrowing()
    {
        using var reader = new FocusReader((IUIAutomation)new CUIAutomation());

        var exception = Record.Exception(() => reader.Read());

        Assert.Null(exception);
    }

    [Fact]
    public void DesktopRoot_HasNoTextCaret()
    {
        var automation = (IUIAutomation)new CUIAutomation();
        var root = automation.GetRootElement();
        try
        {
            Assert.Null(UiaTextCaret.Read(root));
        }
        finally
        {
            Marshal.ReleaseComObject(root);
            Marshal.ReleaseComObject(automation);
        }
    }
}
