using ZoomIN.Core.Overlay;

namespace ZoomIN.Core.Tests.Overlay;

/// <summary>Idle frames (M14 step 3): only a frame where nothing changed is skipped.</summary>
public sealed class FrameChangesTests
{
    [Fact]
    public void NothingChanged_SkipsTheFrame()
    {
        Assert.False(default(FrameChanges).NeedsRedraw);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void EachChange_ForcesAFrame(int which)
    {
        var changes = new FrameChanges(
            which == 0, which == 1, which == 2, which == 3, which == 4, which == 5, which == 6);

        Assert.True(changes.NeedsRedraw);
    }
}
