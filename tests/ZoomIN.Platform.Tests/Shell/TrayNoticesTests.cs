using ZoomIN.Core.Lifecycle;
using ZoomIN.Platform.Shell;

namespace ZoomIN.Platform.Tests.Shell;

/// <summary>Waiting tray notices and what a click does (M17 A1).</summary>
public sealed class TrayNoticesTests
{
    [Fact]
    public void ANoticeStaysUntilWindowsShowedIt_ThenTheClickIsItsCommand()
    {
        var notices = new TrayNotices();
        notices.Add(new("Hint", "b", TrayCommand.OpenTracking));

        Assert.True(notices.TryPeek(out var notice));
        Assert.True(notices.TryPeek(out _)); // not shown yet (no icon): still there
        Assert.Null(notices.Click);

        notices.Shown(notice);

        Assert.False(notices.TryPeek(out _));
        Assert.Equal(TrayCommand.OpenTracking, notices.Click);
    }

    [Fact]
    public void ALaterNoticeWithoutCommand_TakesOverTheClick()
    {
        var notices = new TrayNotices();
        notices.Add(new("Hint", "b", TrayCommand.OpenTracking));
        notices.Add(new("Update", "c", null));

        notices.TryPeek(out var first);
        notices.Shown(first);
        notices.TryPeek(out var second);
        notices.Shown(second);

        Assert.Null(notices.Click);
    }

    [Fact]
    public void ARefusedNotice_IsDropped_AndKeepsTheClickOfTheLastShown()
    {
        var notices = new TrayNotices();
        notices.Add(new("Hint", "b", TrayCommand.OpenTracking));
        notices.Add(new("Status", "c", null));
        notices.TryPeek(out var first);
        notices.Shown(first);

        notices.TryPeek(out var refused);
        notices.Drop(refused);

        Assert.False(notices.TryPeek(out _));
        Assert.Equal(TrayCommand.OpenTracking, notices.Click);
    }

    [Fact]
    public void WithoutTheIcon_OnlyTheNewestFewWait()
    {
        var notices = new TrayNotices();
        for (var i = 0; i < 10; i++)
        {
            notices.Add(new($"N{i}", "x", null));
        }

        Assert.True(notices.TryPeek(out var oldest));
        Assert.Equal("N6", oldest.Title);
    }
}
