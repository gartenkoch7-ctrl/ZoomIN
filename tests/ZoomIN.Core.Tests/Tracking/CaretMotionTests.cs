using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Tracking;

public sealed class CaretMotionTests
{
    [Fact]
    public void SystemCaretAndLineHighUiaCaret_OnTheSameBaseline_AreTheSamePlace()
    {
        // Values measured in Notepad (Windows 11): system caret vs. UI Automation text range.
        Assert.True(CaretMotion.SamePlace(new PhysicalRect(420, 273, 1, 18), new PhysicalRect(420, 257, 1, 34)));
    }

    [Theory]
    [InlineData(428, 273, 1, 18)] // one character typed
    [InlineData(420, 307, 1, 18)] // next line
    public void TypedOrNewLine_IsAMove(double x, double y, double width, double height)
    {
        Assert.False(CaretMotion.SamePlace(new PhysicalRect(420, 273, 1, 18), new PhysicalRect(x, y, width, height)));
    }

    [Fact]
    public void SubPixelDifferenceBetweenSources_IsTheSamePlace()
    {
        Assert.True(CaretMotion.SamePlace(new PhysicalRect(97, 127, 1, 15), new PhysicalRect(97.6, 127.4, 1, 15)));
    }

    [Theory]
    [InlineData(81, 21, true)] // Excel's active cell as system caret
    [InlineData(8, 16, false)] // console block caret
    [InlineData(1, 23, false)] // ordinary text caret
    public void IsCell_OnlyForCarets_WiderThanHigh(double width, double height, bool cell)
    {
        Assert.Equal(cell, CaretMotion.IsCell(new PhysicalRect(0, 0, width, height)));
    }
}
