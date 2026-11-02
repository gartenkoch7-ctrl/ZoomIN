using ZoomIN.Platform.Cursor;

namespace ZoomIN.Platform.Tests.Cursor;

public sealed class Win32CursorShapeSourceTests
{
    [Fact]
    public void LoadStandard_Arrow_IsLoadedSharpAndStandsForTheStandardSize()
    {
        var source = new Win32CursorShapeSource();

        var small = source.LoadStandard(source.ArrowHandle(), 1);
        var large = source.LoadStandard(source.ArrowHandle(), 4);

        Assert.NotNull(small);
        Assert.NotNull(large);
        Assert.Equal(small.BaseSize, large.BaseSize);
        Assert.True(large.Width > small.Width, $"{large.Width} > {small.Width}");
        Assert.Equal(source.ArrowHandle(), large.Handle);
        Assert.Contains(large.Pixels, b => b != 0);
    }
}
