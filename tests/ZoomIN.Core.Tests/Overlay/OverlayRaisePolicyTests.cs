using ZoomIN.Core.Overlay;

namespace ZoomIN.Core.Tests.Overlay;

public sealed class OverlayRaisePolicyTests
{
    [Theory]
    [InlineData(WindowEventKind.Foreground)]
    [InlineData(WindowEventKind.MenuPopupStart)]
    [InlineData(WindowEventKind.Shown)]
    public void NewTopLevelWindow_RaisesVisibleOverlay(WindowEventKind kind)
    {
        Assert.True(OverlayRaisePolicy.ShouldRaise(kind, isWholeWindow: true, isTopLevel: true, overlayVisible: true));
    }

    [Fact]
    public void OtherEvents_AreIgnored()
    {
        Assert.False(OverlayRaisePolicy.ShouldRaise(WindowEventKind.Other, true, true, true));
    }

    [Fact]
    public void ChildObjects_AreIgnored()
    {
        // The caret or a list item being shown fires the same event, but never changes z-order.
        Assert.False(OverlayRaisePolicy.ShouldRaise(WindowEventKind.Shown, isWholeWindow: false, true, true));
    }

    [Fact]
    public void ChildWindows_AreIgnored()
    {
        Assert.False(OverlayRaisePolicy.ShouldRaise(WindowEventKind.Shown, true, isTopLevel: false, true));
    }

    [Fact]
    public void HiddenOverlay_IsNotRaised()
    {
        Assert.False(OverlayRaisePolicy.ShouldRaise(WindowEventKind.MenuPopupStart, true, true, overlayVisible: false));
    }
}
