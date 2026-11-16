using System.Drawing;
using Microsoft.Extensions.Logging.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.WindowDrag;
using ZoomIN.Platform.Tests.WindowFilters;
using ZoomIN.Platform.WindowDrag;

namespace ZoomIN.Platform.Tests.WindowDrag;

/// <summary>
/// The hit test that tells move from resize (G1 mini-spike), against the test's own window with a
/// system title bar. Only messages to that window: no input, no other app is touched.
/// </summary>
[Collection(OnScreenTests.Name)]
public sealed class DragHitTestTests
{
    [Fact]
    public void TitleBar_IsAMove_BorderIsAResize_ClientIsUnknown()
    {
        using var window = TestWindow.WhiteWithTitleBar(new PhysicalRect(240, 240, 420, 300));
        Assert.True(PInvoke.GetWindowRect((HWND)window.Handle, out var frame));
        var client = new Point(0, 0);
        Assert.True(PInvoke.ClientToScreen((HWND)window.Handle, ref client));
        var middle = (frame.left + frame.right) / 2;

        // Halfway between the frame's top and the client area lies the title bar.
        var caption = DragHitTest.At(window.Handle, new PhysicalPoint(middle, (frame.top + client.Y) / 2));
        var border = DragHitTest.At(window.Handle, new PhysicalPoint(frame.right - 2, (frame.top + frame.bottom) / 2));
        var inside = DragHitTest.At(window.Handle, new PhysicalPoint(middle, (client.Y + frame.bottom) / 2));

        Assert.Equal(WindowDragKind.Move, DragHitTest.KindOf(caption));
        Assert.Equal(WindowDragKind.Resize, DragHitTest.KindOf(border));
        Assert.Equal(WindowDragKind.Unknown, DragHitTest.KindOf(inside));
    }

    [Theory]
    [InlineData(2, WindowDragKind.Move)] // title bar
    [InlineData(4, WindowDragKind.Resize)] // size grip
    [InlineData(10, WindowDragKind.Resize)] // left border
    [InlineData(17, WindowDragKind.Resize)] // bottom-right corner
    [InlineData(1, WindowDragKind.Unknown)] // client area: apps that start the move themselves
    [InlineData(18, WindowDragKind.Unknown)] // border that cannot size
    [InlineData(-1, WindowDragKind.Unknown)]
    [InlineData(DragHitTest.NoAnswer, WindowDragKind.Unknown)]
    public void KindOf_TellsMoveFromResize(int hitTest, WindowDragKind expected) =>
        Assert.Equal(expected, DragHitTest.KindOf(hitTest));

    [Fact]
    public void GoneWindow_GivesNoAnswer() =>
        Assert.Equal(DragHitTest.NoAnswer, DragHitTest.At(0x7FFF_0001, new PhysicalPoint(10, 10)));

    [Fact]
    public void Source_StartsAndStops_WithoutDrags()
    {
        var signals = 0;
        var source = new Win32WindowDragSource(NullLogger<Win32WindowDragSource>.Instance);

        var exception = Record.Exception(() =>
        {
            source.Start(_ => signals++, () => true);
            source.Start(_ => signals++, () => true); // a second start does nothing
            Thread.Sleep(50);
            source.Dispose();
        });

        Assert.Null(exception);
        Assert.Equal(0, signals);
    }
}
