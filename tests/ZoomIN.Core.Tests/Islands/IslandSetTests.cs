using ZoomIN.Core.Islands;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Islands;

/// <summary>The immutable list of at most four islands (M19 B1).</summary>
public sealed class IslandSetTests
{
    [Fact]
    public void Add_UpToFour_ThenFull()
    {
        var set = IslandSet.Empty;
        for (var i = 0; i < IslandRules.MaxCount; i++)
        {
            set = set.Add(At(i * 100))!;
        }

        Assert.True(set.IsFull);
        Assert.Null(set.Add(At(900)));
        Assert.Equal([1, 2, 3, 4], set.Items.Select(island => island.Id));
    }

    [Fact]
    public void Ids_AreNotReused_AfterRemove()
    {
        var set = IslandSet.Empty.Add(At(0))!.Add(At(100))!;

        set = set.RemoveAt(0).Add(At(200))!;

        Assert.Equal([2, 3], set.Items.Select(island => island.Id));
        Assert.Equal(1, set.IndexOf(3));
        Assert.Equal(-1, set.IndexOf(1));
    }

    [Fact]
    public void Replace_KeepsTheId()
    {
        var set = IslandSet.Empty.Add(At(0))!;

        set = set.Replace(0, At(500) with { Id = 77 });

        Assert.Equal(1, set[0].Id);
        Assert.Equal(500, set[0].Source.X);
    }

    [Fact]
    public void From_TakesTheFirstFour_SkipsBrokenEntries_AndFitsKnownMonitors()
    {
        var islands = new List<Island>
        {
            At(0) with { MonitorId = string.Empty },
            null!,
            At(0) with { Source = new PhysicalRect(-50, 0, 100, 100) },
            At(100),
            At(200) with { MonitorId = @"\\.\GONE" },
            At(300),
            At(400),
        };

        var set = IslandSet.From(islands, new IslandMonitors().BoundsOf);

        Assert.Equal(4, set.Count);
        Assert.Equal([1, 2, 3, 4], set.Items.Select(island => island.Id));
        Assert.Equal(0, set[0].Source.X);
        Assert.Equal(@"\\.\GONE", set[2].MonitorId); // unknown monitor: kept as saved for when it comes back
        Assert.Equal(5, set.NextId);
    }

    [Fact]
    public void HasIslandOn_ChecksTheMonitor()
    {
        var set = IslandSet.Empty.Add(At(0))!;

        Assert.True(set.HasIslandOn(IslandMonitors.First));
        Assert.False(set.HasIslandOn(IslandMonitors.Second));
    }

    [Fact]
    public void RemoveAt_OutOfRange_Throws()
    {
        var set = IslandSet.Empty.Add(At(0))!;

        Assert.Throws<ArgumentOutOfRangeException>(() => set.RemoveAt(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => set.RemoveAt(-1));
    }

    private static Island At(double x) =>
        new(0, IslandMonitors.First, new PhysicalRect(x, 0, 50, 50), new PhysicalPoint(x, 100), 2);
}
