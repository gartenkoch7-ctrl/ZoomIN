using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.App.Pointer;
using ZoomIN.App.Tests.Overview;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.PointerFence;
using ZoomIN.Core.Viewport;

namespace ZoomIN.App.Tests.Pointer;

/// <summary>Settings → fence filters and key watch (A6): nothing runs while off, a desktop switch starts the key over.</summary>
public sealed class PointerConstraintControllerTests : IDisposable
{
    private readonly OverviewRig _rig = new();
    private readonly FakeFence _fence = new();
    private readonly FakeKeys _keys = new();
    private readonly PointerConstraintController _controller;

    public PointerConstraintControllerTests()
    {
        _controller = new PointerConstraintController(
            _fence, _rig.Monitors, _keys, _rig.Settings, TimeProvider.System, NullLogger<PointerConstraintController>.Instance);
    }

    public void Dispose()
    {
        _controller.Dispose();
        _rig.Dispose();
    }

    [Fact]
    public void Factory_RegistersNothing()
    {
        _controller.Start();

        Assert.Empty(_fence.Filters);
        Assert.Null(_keys.Key);
    }

    [Fact]
    public void Buffer_AddsItsFilterWithTheMonitors()
    {
        _controller.Start();
        Set(new PointerConstraintOptions { Buffer = EdgeBufferLevel.Medium });

        Assert.Equal([_controller.Buffer], _fence.Filters);
        Assert.Equal(new PhysicalPoint(1919, 10), _controller.Buffer.Filter(0, new PhysicalPoint(1919, 10), new PhysicalPoint(1925, 10)));

        Set(new PointerConstraintOptions());
        Assert.Empty(_fence.Filters);
    }

    [Fact]
    public void AxisLock_WatchesTheKeyAndAddsItsFilter()
    {
        _controller.Start();
        Set(new PointerConstraintOptions { AxisLock = true, AxisLockKey = AxisLockKey.RightControl });

        Assert.Equal([_controller.Axis], _fence.Filters);
        Assert.Equal(AxisLockKey.RightControl, _keys.Key);
        _keys.Press(true);
        Assert.True(_controller.Axis.IsHeld);

        Set(new PointerConstraintOptions { AxisLockKey = AxisLockKey.RightControl });
        Assert.Empty(_fence.Filters);
        Assert.Null(_keys.Key);
    }

    [Fact]
    public void OtherKey_RestartsTheWatch()
    {
        _controller.Start();
        Set(new PointerConstraintOptions { AxisLock = true });
        Set(new PointerConstraintOptions { AxisLock = true, AxisLockKey = AxisLockKey.LeftAlt });

        Assert.Equal(AxisLockKey.LeftAlt, _keys.Key);
        Assert.Equal(2, _keys.Starts);
        Assert.Single(_fence.Filters);
    }

    [Fact]
    public void OtherSharedSetting_ChangesNothing()
    {
        _controller.Start();
        Set(new PointerConstraintOptions { AxisLock = true });
        _rig.Settings.Update(s => s with { CloseToTrayHintShown = true });

        Assert.Equal(1, _keys.Starts);
    }

    [Fact]
    public void DesktopSwitch_StartsTheKeyOver()
    {
        _controller.Start();
        Set(new PointerConstraintOptions { AxisLock = true, Buffer = EdgeBufferLevel.Light });

        _keys.Press(true);

        _fence.Raise(FenceTrigger.DesktopSwitch);
        Assert.Equal(1, _keys.Resets);
        Assert.False(_controller.Axis.IsHeld);
        Assert.Equal(1, _keys.Starts);

        _fence.Raise(FenceTrigger.DisplayChange);
        Assert.Equal(1, _keys.Resets);
    }

    [Fact]
    public void AxisLockOff_ReleasesAHeldKey()
    {
        _controller.Start();
        Set(new PointerConstraintOptions { AxisLock = true });
        _keys.Press(true);
        _keys.Silent = true; // a watch that misses the release on Stop

        Set(new PointerConstraintOptions());

        Assert.False(_controller.Axis.IsHeld);
    }

    [Fact]
    public void FailingFence_TurnsEverythingOff()
    {
        _controller.Start();
        _fence.FailNextAdd = true;
        Set(new PointerConstraintOptions { AxisLock = true, Buffer = EdgeBufferLevel.Light });

        Assert.Empty(_fence.Filters);
        Assert.Null(_keys.Key);
    }

    [Fact]
    public void Dispose_RemovesEverything()
    {
        _controller.Start();
        Set(new PointerConstraintOptions { AxisLock = true, Buffer = EdgeBufferLevel.Strong });

        _controller.Dispose();

        Assert.Empty(_fence.Filters);
        Assert.Null(_keys.Key);
    }

    [Fact]
    public void FailingKeyStop_StillRemovesTheFilter()
    {
        _controller.Start();
        Set(new PointerConstraintOptions { AxisLock = true });
        _keys.FailNextStop = true;

        Set(new PointerConstraintOptions());

        Assert.Empty(_fence.Filters);
    }

    [Fact]
    public void FailedRequest_IsNotRetriedByUnrelatedChanges()
    {
        _controller.Start();
        _fence.FailNextAdd = true;
        Set(new PointerConstraintOptions { Buffer = EdgeBufferLevel.Light });

        _rig.Settings.Update(s => s with { CloseToTrayHintShown = true });
        Assert.Empty(_fence.Filters);

        Set(new PointerConstraintOptions { Buffer = EdgeBufferLevel.Medium });
        Assert.Single(_fence.Filters);
    }

    [Fact]
    public void LateApplyAfterDispose_InstallsNothing()
    {
        _controller.Start();
        _controller.Dispose();

        _controller.Apply(new PointerConstraintOptions { AxisLock = true, Buffer = EdgeBufferLevel.Light });

        Assert.Empty(_fence.Filters);
        Assert.Null(_keys.Key);
    }

    private void Set(PointerConstraintOptions options) => _rig.Settings.Update(s => s with { PointerConstraint = options });
}
