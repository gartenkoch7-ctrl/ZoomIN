using ZoomIN.Core.Modes;
using ZoomIN.Core.Overview;

namespace ZoomIN.Core.Tests.Overview;

/// <summary>Tap, hold, Esc and cancel of the overview (B2) as pure state.</summary>
public sealed class OverviewModelTests
{
    private const string Monitor = @"\\.\DISPLAY1";

    [Fact]
    public void Tap_KeepsTheOverview_AndTheNextPress_ZoomsIn()
    {
        var (model, clock, log) = Create();

        model.Press(Monitor, ZoomMode.Fullscreen, 4);
        clock.Advance(TimeSpan.FromMilliseconds(120));
        model.Release();

        Assert.Equal(OverviewPhase.Tapped, model.Phase);
        Assert.Equal(["started 1"], log);

        clock.Advance(TimeSpan.FromSeconds(5));
        model.Press(Monitor, ZoomMode.Fullscreen, 4);

        Assert.Null(model.Current);
        Assert.Equal(OverviewPhase.Off, model.Phase);
        Assert.Equal(["started 1", "ended 1 ZoomIn tap"], log);
    }

    [Fact]
    public void Hold_ZoomsIn_OnRelease()
    {
        var (model, clock, log) = Create();
        model.Press(Monitor, ZoomMode.Lens, 4);

        clock.Advance(OverviewTiming.TapLimit);
        model.Release();

        Assert.Null(model.Current);
        Assert.Equal(["started 1", "ended 1 ZoomIn hold"], log);
    }

    [Fact]
    public void Release_IsIgnored_UnlessTheShortcutIsDown()
    {
        var (model, _, log) = Create();
        model.Release();
        model.Press(Monitor, ZoomMode.Fullscreen, 2);
        model.Release();

        model.Release(); // a second release signal changes nothing

        Assert.Equal(OverviewPhase.Tapped, model.Phase);
        Assert.Equal(["started 1"], log);
    }

    [Fact]
    public void HookSignals_UseTheirOwnTimestamp_AndOnlyReachTheirOverview()
    {
        var (model, clock, log) = Create();
        model.Press(Monitor, ZoomMode.Fullscreen, 3);
        var id = model.Current!.Id;
        var releasedAt = clock.GetTimestamp() + TimeSpan.FromMilliseconds(100).Ticks;
        clock.Advance(TimeSpan.FromSeconds(2)); // delivered late

        model.Release(id + 1, releasedAt);
        Assert.Equal(OverviewPhase.Pressed, model.Phase);
        model.Release(id, releasedAt);
        Assert.Equal(OverviewPhase.Tapped, model.Phase);

        model.Escape(id);
        model.Press(Monitor, ZoomMode.Fullscreen, 3);
        clock.Advance(TimeSpan.FromSeconds(2));
        model.Release(model.Current!.Id, releasedAt: null); // time unknown: a tap
        Assert.Equal(OverviewPhase.Tapped, model.Phase);
        id = model.Current!.Id;

        Assert.False(model.Escape(id + 1));
        Assert.True(model.Escape(id));
        Assert.Equal(["started 1", "ended 1 Escape tap", "started 2", "ended 2 Escape tap"], log);
    }

    [Fact]
    public void Escape_EndsTheOverview_ButNotWhenNoneRuns()
    {
        var (model, clock, log) = Create();
        Assert.False(model.Escape());

        model.Press(Monitor, ZoomMode.Fullscreen, 3);
        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.True(model.Escape());
        Assert.Equal(["started 1", "ended 1 Escape hold"], log);
        Assert.False(model.Escape());
    }

    [Fact]
    public void Cancel_EndsOnlyTheGivenOverview()
    {
        var (model, _, log) = Create();
        model.Press(Monitor, ZoomMode.Fullscreen, 3);
        var first = model.Current!;
        model.Release();
        model.Press(Monitor, ZoomMode.Fullscreen, 3);
        model.Press(Monitor, ZoomMode.Fullscreen, 3);

        Assert.False(model.Cancel(first));
        Assert.True(model.Cancel(model.Current!));
        Assert.Equal(["started 1", "ended 1 ZoomIn tap", "started 2", "ended 2 Cancelled tap"], log);
    }

    [Theory]
    [InlineData(ZoomMode.Docked, 4)]
    [InlineData(ZoomMode.Fullscreen, 1)]
    [InlineData(ZoomMode.Lens, 1)]
    public void Press_DoesNothing_InDockedMode_OrWithoutZoom(ZoomMode mode, double level)
    {
        var (model, _, log) = Create();

        model.Press(Monitor, mode, level);

        Assert.Null(model.Current);
        Assert.Empty(log);
        Assert.False(OverviewModel.CanStart(mode, level));
    }

    [Fact]
    public void Session_KeepsMonitorModeLevelAndStart()
    {
        var (model, clock, _) = Create();
        model.Press(Monitor, ZoomMode.Lens, 6);

        var session = model.Current!;

        Assert.Equal(Monitor, session.MonitorId);
        Assert.Equal(ZoomMode.Lens, session.Mode);
        Assert.Equal(6, session.LevelBefore);
        Assert.Equal(clock.GetTimestamp(), session.StartedAt);
        Assert.Throws<ArgumentNullException>(() => model.Press(null!, ZoomMode.Lens, 2));
        Assert.Throws<ArgumentNullException>(() => new OverviewModel(null!));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(299, true)]
    [InlineData(300, false)]
    [InlineData(1000, false)]
    public void TapLimit_Is300Milliseconds(int milliseconds, bool tap) =>
        Assert.Equal(tap, OverviewTiming.IsTap(TimeSpan.FromMilliseconds(milliseconds)));

    private static (OverviewModel Model, ManualClock Clock, List<string> Log) Create()
    {
        var clock = new ManualClock();
        var model = new OverviewModel(clock);
        var log = new List<string>();
        model.Started += s => log.Add($"started {s.Id}");
        model.Ended += e => log.Add($"ended {e.Session.Id} {e.End} {(e.Held ? "hold" : "tap")}");
        return (model, clock, log);
    }
}
