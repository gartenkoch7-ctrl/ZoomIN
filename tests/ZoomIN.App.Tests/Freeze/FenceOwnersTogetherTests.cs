using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.App.Islands;
using ZoomIN.App.Localization;
using ZoomIN.App.Pointer;
using ZoomIN.App.Tests.Islands;
using ZoomIN.App.Tests.Overview;
using ZoomIN.App.Tests.Pointer;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.PointerFence;
using static ZoomIN.App.Tests.Freeze.ThreeMonitors;

namespace ZoomIN.App.Tests.Freeze;

/// <summary>
/// M19 integration: all three owners of the pointer fence at once – a frozen monitor (G2), the island
/// edit mode (B1) and the axis lock with its held key (A6). Win+L, UAC, Ctrl+Alt+Del or unplugging must give
/// everything back.
/// </summary>
public sealed class FenceOwnersTogetherTests : IDisposable
{
    private readonly FreezeRig _rig = new();
    private readonly FakeKeys _keys = new();
    private readonly PointerConstraintController _constraint;
    private readonly IslandController _islands;

    public FenceOwnersTogetherTests()
    {
        _constraint = new PointerConstraintController(
            _rig.Fence, _rig.Monitors, _keys, _rig.Settings, TimeProvider.System, NullLogger<PointerConstraintController>.Instance);
        var texts = AppTexts.CreateLocalizer();
        texts.SetLanguage(Core.Settings.AppLanguage.English);
        _islands = new IslandController(_rig.Settings, _rig.Monitors, new FakeIslandInput(), _rig.Fence, new RecordingAnnouncer(), texts,
            new TimerClock(), NullLogger<IslandController>.Instance);

        _rig.Models.Zoom.Activate(ZoomMode.Fullscreen, Two, [One, Two, Three]);
        _rig.PointerTo(Two);
        _rig.Controller.ToggleFreeze();
        _constraint.Start();
        _rig.Settings.Update(s => s with
        {
            PointerConstraint = new PointerConstraintOptions { AxisLock = true, Buffer = EdgeBufferLevel.Light },
        });
        _keys.Press(true);
        _islands.ToggleEdit();
    }

    public void Dispose()
    {
        _islands.Dispose();
        _constraint.Dispose();
        _rig.Dispose();
    }

    [Theory]
    [InlineData(FenceTrigger.DesktopSwitch)]
    [InlineData(FenceTrigger.SessionUnlock)]
    public void DesktopSwitch_LiftsTheFreeze_EndsEditing_AndLetsTheKeyGo(FenceTrigger trigger)
    {
        Assert.True(_rig.Frozen.IsFrozen(Two));
        Assert.True(_islands.IsEditing);
        Assert.True(_constraint.Axis.IsHeld);

        _rig.Fence.Raise(trigger);

        Assert.False(_rig.Frozen.Any);
        Assert.Empty(_rig.Fence.Forbidden);
        Assert.Equal(1, _keys.Resets);
        Assert.False(_constraint.Axis.IsHeld);
        Assert.True(SpinWait.SpinUntil(() => !_islands.IsEditing, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void UnpluggingTheFrozenMonitor_LiftsIt_EndsEditing_AndKeepsTheHeldKey()
    {
        ((ThreeMonitors)_rig.Monitors).Unplug(Two);

        _rig.Fence.Raise(FenceTrigger.DisplayChange);

        Assert.False(_rig.Frozen.Any);
        Assert.Empty(_rig.Fence.Forbidden);
        Assert.Equal(0, _keys.Resets); // a display change does not touch the keys
        Assert.True(_constraint.Axis.IsHeld);
        Assert.True(SpinWait.SpinUntil(() => !_islands.IsEditing, TimeSpan.FromSeconds(5)));
    }
}
