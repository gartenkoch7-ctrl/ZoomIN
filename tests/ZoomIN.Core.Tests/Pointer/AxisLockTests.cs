using ZoomIN.Core.Pointer;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Tests.Pointer;

/// <summary>Axis lock (A6): free for the first 8 px, then only along the larger axis, free again on release.</summary>
public sealed class AxisLockTests
{
    private readonly AxisLock _lock = new();

    [Fact]
    public void KeyNotHeld_LetsEveryMoveThrough()
    {
        Assert.Equal(new PhysicalPoint(130, 170), Move(100, 100, 130, 170));
        Assert.Equal(LockAxis.None, _lock.Axis);
        Assert.Equal(PointerFilterOrder.AxisLock, _lock.Order);
    }

    [Fact]
    public void BelowTheThreshold_MovesFreely()
    {
        _lock.SetHeld(true);

        Assert.Equal(new PhysicalPoint(105, 103), Move(100, 100, 105, 103));
        Assert.Equal(new PhysicalPoint(107, 96), Move(105, 103, 107, 96));
        Assert.Equal(LockAxis.None, _lock.Axis);
    }

    [Fact]
    public void LargerHorizontalMove_LocksTheRow()
    {
        _lock.SetHeld(true);
        Move(100, 100, 104, 102);

        // 8 px to the right, 3 px down: horizontal, back onto the row where the key went down.
        Assert.Equal(new PhysicalPoint(108, 100), Move(104, 102, 108, 103));
        Assert.Equal(LockAxis.Horizontal, _lock.Axis);
        Assert.Equal(new PhysicalPoint(300, 100), Move(108, 100, 300, 140));
        Assert.Equal(new PhysicalPoint(20, 100), Move(300, 100, 20, 60));
    }

    [Fact]
    public void LargerVerticalMove_LocksTheColumn()
    {
        _lock.SetHeld(true);

        Assert.Equal(new PhysicalPoint(100, 90), Move(100, 100, 102, 90));
        Assert.Equal(LockAxis.Vertical, _lock.Axis);
        Assert.Equal(new PhysicalPoint(100, 400), Move(100, 90, 160, 400));
    }

    [Fact]
    public void Diagonal_GoesToHorizontal()
    {
        _lock.SetHeld(true);

        Assert.Equal(new PhysicalPoint(110, 100), Move(100, 100, 110, 110));
        Assert.Equal(LockAxis.Horizontal, _lock.Axis);
    }

    [Fact]
    public void Release_FreesAtOnce()
    {
        _lock.SetHeld(true);
        Move(100, 100, 120, 100);
        _lock.SetHeld(false);

        Assert.Equal(new PhysicalPoint(130, 160), Move(120, 100, 130, 160));
        Assert.Equal(LockAxis.None, _lock.Axis);
        Assert.False(_lock.IsHeld);
    }

    [Fact]
    public void NewPress_ChoosesAgainFromTheNewPosition()
    {
        _lock.SetHeld(true);
        Move(100, 100, 120, 100);
        _lock.SetHeld(false);
        _lock.SetHeld(true); // released and pressed again between two moves

        Assert.Equal(new PhysicalPoint(122, 503), Move(120, 500, 122, 503));
        Assert.Equal(new PhysicalPoint(120, 520), Move(122, 503, 125, 520));
        Assert.Equal(LockAxis.Vertical, _lock.Axis);
    }

    [Theory]
    [InlineData(150, 100, 500, 900, 560, 905, 560, 900)] // horizontal lock, pointer moved to another row
    [InlineData(100, 150, 500, 900, 505, 960, 500, 960)] // vertical lock, pointer moved to another column
    public void PointerMovedByOthers_LocksFromThere(double lockX, double lockY, double jumpX, double jumpY, double toX, double toY, double expectX, double expectY)
    {
        _lock.SetHeld(true);
        Move(100, 100, lockX, lockY);

        Assert.Equal(new PhysicalPoint(expectX, expectY), Move(jumpX, jumpY, toX, toY));
    }

    [Fact]
    public void PointerMovedByOthers_BeforeTheAxisIsChosen_MeasuresFromThere()
    {
        _lock.SetHeld(true);
        Move(500, 300, 503, 301);

        // Jumped to another monitor while still free: the next 3 px are no 2500 px swing.
        Assert.Equal(new PhysicalPoint(3003, 901), Move(3000, 900, 3003, 901));
        Assert.Equal(LockAxis.None, _lock.Axis);
        Assert.Equal(new PhysicalPoint(3010, 900), Move(3003, 901, 3010, 902));
    }

    [Fact]
    public void Lock_IsCounted()
    {
        var before = PointerConstraintMetrics.AxisLocks(LockAxis.Vertical);
        _lock.SetHeld(true);
        Move(100, 100, 100, 150);
        Move(100, 150, 100, 200);

        Assert.True(PointerConstraintMetrics.AxisLocks(LockAxis.Vertical) - before >= 1);
    }

    private PhysicalPoint Move(double fromX, double fromY, double toX, double toY) =>
        _lock.Filter(0, new PhysicalPoint(fromX, fromY), new PhysicalPoint(toX, toY));
}
