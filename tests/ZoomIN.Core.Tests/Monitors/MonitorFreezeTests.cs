using ZoomIN.Core.Modes;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Viewport;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.Tests.Monitors;

/// <summary>The frozen-monitor state in the zoom model (G2): swapped atomically, read lock-free and without allocation.</summary>
public sealed class MonitorFreezeTests
{
    private const string First = @"\\.\DISPLAY1";
    private const string Second = @"\\.\DISPLAY2";

    [Fact]
    public void Freeze_Unfreeze_AndQueries()
    {
        var model = new ZoomModel(new ZoomLevels(), ZoomMode.Fullscreen, true, 0);
        var changes = 0;
        model.Frozen.Changed += () => changes++;
        Assert.False(model.Frozen.Any);
        Assert.Same(FreezeState.Empty, model.Frozen.State);

        var first = Frozen(First);
        model.Frozen.Freeze(first);
        model.Frozen.Freeze(Frozen(Second));

        Assert.True(model.Frozen.IsFrozen(First));
        Assert.Same(first, model.Frozen.For(First));
        Assert.Equal(2, model.Frozen.State.Count);
        Assert.Equal([new PhysicalRect(0, 0, 100, 100), new PhysicalRect(0, 0, 100, 100)], model.Frozen.State.Areas());

        var removed = model.Frozen.Unfreeze(m => m.Id == First);
        Assert.Equal([first], removed);
        Assert.False(model.Frozen.IsFrozen(First));
        Assert.True(model.Frozen.IsFrozen(Second));

        Assert.Empty(model.Frozen.Unfreeze(m => m.Id == First));
        Assert.Single(model.Frozen.UnfreezeAll());
        Assert.Same(FreezeState.Empty, model.Frozen.State);
        Assert.Equal(4, changes); // two freezes, two lifts; the empty lift raises nothing
    }

    [Fact]
    public void FreezingAgain_ReplacesTheOldFreeze()
    {
        var freeze = new MonitorFreeze();
        var old = Frozen(First);
        freeze.Freeze(old);
        var again = Frozen(First);
        freeze.Freeze(again);

        Assert.Equal(1, freeze.State.Count);
        Assert.Same(again, freeze.For(First));
        Assert.Throws<ArgumentNullException>(() => freeze.Freeze(null!));
        Assert.Throws<ArgumentNullException>(() => freeze.Unfreeze(null!));
    }

    [Fact]
    public void FrozenMonitor_KeepsTheFirstAnchor_AndClampsItsLevel()
    {
        var monitor = new FrozenMonitor(Second, default, 40, ZoomMode.Lens, LensSize.Default, DockLayout.Default, true, RgbColor.Cyan);

        Assert.Equal(ZoomLimits.Max, monitor.Level);
        Assert.Equal(2, monitor.Number);

        Assert.Equal(new PhysicalPoint(10, 20), monitor.AnchorOr(new PhysicalPoint(10, 20)));
        Assert.Equal(new PhysicalPoint(10, 20), monitor.AnchorOr(new PhysicalPoint(30, 40)));
        Assert.Equal("still", monitor.ImageKind);
        Assert.Throws<ArgumentNullException>(() => new FrozenMonitor(null!, default, 2, ZoomMode.Lens, LensSize.Default, DockLayout.Default, false, default));
    }

    [Fact]
    public void AnchorOr_KeepsTheFirstAnchor_WithoutAllocating()
    {
        var monitors = Enumerable.Range(0, 1_000).Select(_ => Frozen(First)).ToArray();
        _ = Frozen(First).AnchorOr(new PhysicalPoint(1, 1)); // JIT

        var before = GC.GetAllocatedBytesForCurrentThread();
        var sum = 0.0;
        foreach (var monitor in monitors)
        {
            sum += monitor.AnchorOr(new PhysicalPoint(3, 4)).X + monitor.AnchorOr(new PhysicalPoint(9, 9)).Y;
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(1_000 * 7, sum);
    }

    [Fact]
    public void PerFrameReads_AreLockFree_AndDoNotAllocate()
    {
        var model = new ZoomModel(new ZoomLevels(), ZoomMode.Fullscreen, true, 0);
        var monitor = Frozen(Second);
        monitor.AnchorOr(new PhysicalPoint(1, 2));
        model.Frozen.Freeze(Frozen(First));
        model.Frozen.Freeze(monitor);

        // A writer keeps swapping states meanwhile: readers always see a whole state.
        var stop = false;
        var writer = new Thread(() =>
        {
            while (!Volatile.Read(ref stop))
            {
                model.Frozen.Unfreeze(m => m.Id == First);
                model.Frozen.Freeze(Frozen(First));
            }
        });
        writer.Start();

        var found = 0.0;
        for (var i = 0; i < 100; i++)
        {
            found += Read(model);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
        {
            found += Read(model);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Volatile.Write(ref stop, true);
        writer.Join();
        Assert.Equal(0, allocated);
        Assert.Equal(10_100 * 3, found);
    }

    // Second is always frozen with its anchor (1, 2).
    private static double Read(ZoomModel model) =>
        model.Frozen.For(Second) is { } frozen && frozen.AnchorOr(default) is var anchor ? anchor.X + anchor.Y : 0;

    private static FrozenMonitor Frozen(string id) =>
        new(id, new PhysicalRect(0, 0, 100, 100), 2, ZoomMode.Fullscreen, LensSize.Default, DockLayout.Default, false, RgbColor.Cyan);
}
